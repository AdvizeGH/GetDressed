using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace GetDressed.Framework
{
    public class Alert
    {
        int x;
        int y;
        string text;

        public float timeLeft;
        public float delayTimeLeft;
        public float transparency = 1f;
        public bool fadeIn;

        Texture2D texture = null;
        Rectangle sourceRect = new Rectangle(0, 0, 0, 0);

        public Alert(int x, int y, string text, float timeLeft, bool fadeIn, float delayTimeLeft = 0f)
        {
            this.x = x; this.y = y; this.text = text; this.timeLeft = timeLeft; this.fadeIn = fadeIn; this.delayTimeLeft = delayTimeLeft;
        }

        public Alert(Texture2D texture, Rectangle sourceRect, int x, int y, string text, float timeLeft, bool fadeIn, float delayTimeLeft = 0f)
        {
            this.texture = texture; this.sourceRect = sourceRect; this.x = x; this.y = y; this.text = text; this.timeLeft = timeLeft; this.fadeIn = fadeIn; this.delayTimeLeft = delayTimeLeft;
        }

        public bool update(GameTime time)
        {
            if (delayTimeLeft > 0f)
            {
                delayTimeLeft -= time.ElapsedGameTime.Milliseconds;
                return false;
            }
            timeLeft -= time.ElapsedGameTime.Milliseconds;
            if (timeLeft < 0f)
            {
                transparency -= 0.02f;
                if (transparency < 0f)
                {
                    return true;
                }
            }
            else if (fadeIn)
            {
                transparency = Math.Min(transparency + 0.02f, 1f);
            }
            return false;
        }

        public void draw(SpriteBatch b, SpriteFont font)
        {
            if (delayTimeLeft > 0f)
            {
                return;
            }
            int num = (int)font.MeasureString(text).X;

            b.Draw(Game1.mouseCursors, new Vector2(x + 370, y - 50), new Rectangle?(new Rectangle(319, 360, 1, 24)), Color.White * transparency, 0f, Vector2.Zero, new Vector2(num, Game1.pixelZoom), SpriteEffects.None, 0.0001f);

            b.Draw(Game1.mouseCursors, new Vector2(x + 275, y - 50), new Rectangle?(new Rectangle(293, 360, 24, 24)), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);

            b.Draw(Game1.mouseCursors, new Vector2(x + 370 + num, y - 50), new Rectangle?(new Rectangle(322, 360, 7, 24)), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);

            if (texture != null)
            {
                b.Draw(texture, new Vector2(x + 290, y - 35), new Rectangle?(sourceRect), Color.White * transparency, 0f, Vector2.Zero, Game1.pixelZoom, SpriteEffects.None, 0.0001f);
            }

            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(x + 375, y - 15), Color.Black * transparency, 1f, 1f, -1, -1, transparency, 3);
        }
    }
}
