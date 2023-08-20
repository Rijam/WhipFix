using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using Terraria;
using Terraria.ModLoader;
namespace WhipFix
{
    public class WhipFix : Mod
    {
        /*
        Ok so the vanilla code pre 1.4.4 was

        if (this.ai[0] >= timeToFlyOut || player.itemAnimation == 0)
        {
            this.Kill();
            return;
        }

        but after 1.4.4 it was changed to 

        if (this.ai[0] >= timeToFlyOut)
        {
            this.Kill();
            return;
        }

        and the player.itemAnimation == 0 is what this IL edit relied on.

        The IL edit was: if player.itemAnimation == 1, then set the player.itemAnimation = 0.
            That caused the whip to be killed which allowed you to swap to another whip.

        Instead of redoing the IL edit, I just made a detour which sets player.itemAnimation = 0 if player.itemAnimation == 1 before any whip code runs.
        So the vanilla code looks like:

            private void AI_165_Whip()
            {
                if (Main.player[owner].itemAnimation == 1) {
                    Main.player[owner].itemAnimation = 0;
                }

                Player player = Main.player[owner];
                rotation = velocity.ToRotation() + (float)Math.PI / 2f;
                ai[0] += 1f;
                GetWhipSettings(this, out var timeToFlyOut, out var _, out var _);
                base.Center = Main.GetPlayerArmPosition(this) + velocity * (ai[0] - 1f);
                spriteDirection = ((!(Vector2.Dot(velocity, Vector2.UnitX) < 0f)) ? 1 : (-1));

                if (ai[0] >= timeToFlyOut) {
                    Kill();
                    return;
                }

                player.heldProj = whoAmI;
                player.MatchItemTimeToItemAnimation();
                ...
            }

        The player.MatchItemTimeToItemAnimation(); is new too, but it just sets player.itemTime = player.itemAnimation

        - Rijam
        */

        public override void Load() {
            base.Load();
            // 判断代码都是一样的，直接用同一个IL编辑
            //Terraria.IL_Projectile.AI_165_Whip += WhipPatch;
            //Terraria.IL_Projectile.AI_019_Spears += WhipPatch;
            Terraria.On_Projectile.AI_165_Whip += Projectile_Hook_AI_165_Whip;
        }

        private delegate void orig_Projectile_AI_165_Whip(Projectile self);

        private static void Projectile_Hook_AI_165_Whip(On_Projectile.orig_AI_165_Whip orig, Projectile self)
        {
            if (Main.player[self.owner].itemAnimation == 1)
            {
                Main.player[self.owner].itemAnimation = 0;
            }
            orig(self);
        }

        // Unused now:

        // tML大概是itemAnimation为0和1时各判断了一次出手，丢了两个鞭子，刚好使运行到SmartSelectLookup的时候没有itemAnimation为0的空挡期了，切鞭也不能用了
        private void WhipPatch(ILContext il) {
            var c = new ILCursor(il);
            c.GotoNext(MoveType.After, i => i.MatchLdloc(0));
            c.GotoNext(MoveType.After, i => i.MatchLdfld(typeof(Player), nameof(Player.itemAnimation)));
            c.Emit(OpCodes.Ldloc_0);
            c.EmitDelegate<Func<int, Player, int>>((returnValue, player) => {
                if (returnValue == 1) {
                    player.itemAnimation = 0; // 设为0才会在SmartSelectLookup里切换selectedItem
                    return 0;
                }
                return returnValue;
            });
        }
    }
}