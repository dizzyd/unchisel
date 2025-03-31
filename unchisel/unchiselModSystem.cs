using unchisel.items;
using Vintagestory.API.Common;

namespace unchisel;

public class unchiselModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterItemClass(Mod.Info.ModID + ".unchisel", typeof(ItemUnchisel));
    }
}