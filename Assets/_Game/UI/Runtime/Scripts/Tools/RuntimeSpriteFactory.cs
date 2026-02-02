using UnityEngine;

namespace SeasonalBastion
{
    internal static class RuntimeSpriteFactory
    {
        private static Sprite _white;

        public static Sprite WhiteSprite
        {
            get
            {
                if (_white != null) return _white;

                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
                tex.name = "__RuntimeWhiteTex";
                tex.SetPixel(0, 0, Color.white);
                tex.Apply(false, true);

                _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                _white.name = "__RuntimeWhiteSprite";
                return _white;
            }
        }
    }
}
