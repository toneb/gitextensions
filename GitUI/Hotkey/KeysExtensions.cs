using System.Globalization;
#if AVALONIA
using Avalonia.Input;
#endif

namespace GitUI.Hotkey
{
    public static class KeysExtensions
    {
#if !AVALONIA
        /// <summary>
        /// Strips the modifier from KeyData.
        /// </summary>
        public static Keys GetKeyCode(this Keys keyData)
        {
            return keyData & Keys.KeyCode;
        }
#endif

        public static bool IsModifierKey(this Keys key)
        {
#if !AVALONIA
            return key == Keys.ShiftKey ||
                   key == Keys.ControlKey ||
                   key == Keys.Alt;
#else
            return key.KeyModifiers != KeyModifiers.None;
#endif
        }

#if !AVALONIA
        public static Keys[] GetModifiers(this Keys key)
        {
            // Retrieve the modifiers, mask away the rest
            Keys modifier = key & Keys.Modifiers;

            List<Keys> modifierList = new();

            void AddIfContains(Keys m)
            {
                if (m == (m & modifier))
                {
                    modifierList.Add(m);
                }
            }

            AddIfContains(Keys.Control);
            AddIfContains(Keys.Shift);
            AddIfContains(Keys.Alt);

            return modifierList.ToArray();
        }
#else
        public static KeyModifiers[] GetModifiers(this Keys key)
        {
            // Retrieve the modifiers, mask away the rest
            List<KeyModifiers> modifierList = new();

            void AddIfContains(KeyModifiers m)
            {
                if (m == (m & key.KeyModifiers))
                {
                    modifierList.Add(m);
                }
            }

            AddIfContains(KeyModifiers.Control);
            AddIfContains(KeyModifiers.Shift);
            AddIfContains(KeyModifiers.Alt);

            return modifierList.ToArray();
        }
#endif

        public static string ToText(this Keys key)
        {
#if !AVALONIA
            return string.Join(
                "+",
                key.GetModifiers()
                    .Union(new[] { key.GetKeyCode() })
                    .Select(k => k.ToFormattedString())
                    .ToArray());
#else
            // TODO: avalonia - handle localization for keys
            return key.ToString();
#endif
        }

#if !AVALONIA
        public static string? ToFormattedString(this Keys key)
        {
            if (key == Keys.Oemcomma)
            {
                return ",";
            }

            if (key == Keys.Decimal)
            {
                return ".";
            }

            // Get the string representation
            var str = key.ToCultureSpecificString();

            // Strip the leading 'D' if it's a Decimal Key (D1, D2, ...)
            if (str?.Length is 2 && str[0] == 'D')
            {
                str = str[1].ToString();
            }

            return str;
        }
#endif

        public static string ToShortcutKeyDisplayString(this Keys key)
        {
            return key.ToText();
        }

#if !AVALONIA
        private static string? ToCultureSpecificString(this Keys key)
        {
            if (key == Keys.None)
            {
                return null;
            }

            // var str = key.ToString(); // OLD: this is culture unspecific
            var culture = CultureInfo.CurrentCulture; // TODO: replace this with the GitExtensions language setting

            // for modifier keys this yields for example "Ctrl+None" thus we have to strip the rest after the +
            return new KeysConverter().ConvertToString(null, culture, key)?.SubstringUntil('+');
        }
#endif
    }
}
