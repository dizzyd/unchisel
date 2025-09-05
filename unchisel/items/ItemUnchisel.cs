using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace unchisel.items;

internal class ItemUnchisel : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        if (handling == EnumHandHandling.PreventDefault)
        {
            api.Logger.Warning("unchisel.OnHeldInteractStart intercepted (side: {0})", byEntity.World.Side);
            (api as ICoreClientAPI)?.TriggerIngameError(this, "uhh", "Handled, I guess");
            return;
        }

        // It's possible that the block selection is null; not sure why this happens, but the original Chisel
        // code also does this check, so I assume it's legit.
        if (blockSel?.Position == null) return;
        
        // Must be holding a hammer
        ItemSlot leftSlot = byEntity.LeftHandItemSlot; 
        if (leftSlot?.Itemstack?.Collectible?.Tool is not EnumTool.Hammer)
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "nohammer", Lang.Get("Requires a hammer in the off hand"));
            handling = EnumHandHandling.PreventDefault;
            return;
        }
        
        // Target block must be breakable by the player
        IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
        if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "notallowed", "Block is claimed by another player");
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        // If the target block is reinforced, show an error to user and make them use the plumb/square to remove it
        ModSystemBlockReinforcement modBre = byEntity.World.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
        if (modBre != null && modBre.IsReinforced(blockSel.Position))
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "noreinforced", "Block is reinforced");
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        // Targeted block must be chiselable (according to ItemChisel definition which covers a lot of cases)
        // N.B. make sure to use world BlockAccessor, not blockSel.Block as that may not be populated
        //      on the server side instance of this call
        Block microBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        if (!ItemChisel.IsChiselingAllowedFor(api, blockSel.Position, microBlock, byPlayer)) 
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "notchiselable", "Block can not be chiseled");
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        // Target block must also have a materials list
        ItemStack microBlockStack = microBlock.OnPickBlock(byEntity.World, blockSel.Position);
        IntArrayAttribute materials = microBlockStack?.Attributes["materials"] as IntArrayAttribute;
        if (microBlockStack != null && materials == null)
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "nomicroblock", "Block must be already chiseled");
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            // Spawn items that were in the original block
            foreach (int materialId in materials.value)
            {
                Block material = api.World.GetBlock(materialId);
                byEntity.World.SpawnItemEntity(new ItemStack(material, 1), blockSel.Position);
            }

            // Damage the unchisel, based on number of materials that will be returned
            EntityPlayer player = byEntity as EntityPlayer;
            if (player != null)
            {
                int totalDamage = materials.value.Length;
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, player.Player.InventoryManager.ActiveHotbarSlot, totalDamage);
            }
            
            // Delete the old block
            byEntity.World.BlockAccessor.SetBlock(0, blockSel.Position);
            byEntity.World.BlockAccessor.MarkBlockModified(blockSel.Position);
        }

        handling = EnumHandHandling.Handled;
    }
}