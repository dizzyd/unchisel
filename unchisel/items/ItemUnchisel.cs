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

        // Targeted block must be a microblock
        Block microBlock = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        if (microBlock is not BlockMicroBlock)
        {
            (api as ICoreClientAPI)?.TriggerIngameError(this, "nomicroblock", "Block must already be chiseled");
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        if (byEntity.World.Side == EnumAppSide.Server)
        {
            // Spawn items that were in the original block - note that we need to use the block's PickBlock,
            // not just an item stack of the block so that things like planks (which have multiple blocks with different
            // orientations) will return the correct "default" properly.
            ItemStack microBlockStack = microBlock.OnPickBlock(byEntity.World, blockSel.Position);
            IntArrayAttribute materials = microBlockStack.Attributes["materials"] as IntArrayAttribute;
            foreach (int materialId in materials.value)
            {
                Block material = api.World.GetBlock(materialId);
                ItemStack itemToSpawn = material.OnPickBlock(api.World, blockSel.Position);
                byEntity.World.SpawnItemEntity(itemToSpawn, blockSel.Position);
            }

            // Damage the unchisel, based on number of materials that will be returned
            EntityPlayer player = byEntity as EntityPlayer;
            if (player != null)
            {
                int totalDamage = materials.value.Length;
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, player.Player.InventoryManager.ActiveHotbarSlot, totalDamage);
            }
            
            // Break any beams attached to this position; make sure to drop them for the user
            var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null && be.Beams != null)
            {
                for(int i = 0; i < be.Beams.Length; i++)
                {
                    be.BreakBeam(i, true);
                }
            }

            // Delete the old block
            byEntity.World.BlockAccessor.SetBlock(0, blockSel.Position);
            byEntity.World.BlockAccessor.MarkBlockModified(blockSel.Position);
        }

        handling = EnumHandHandling.Handled;
    }
}