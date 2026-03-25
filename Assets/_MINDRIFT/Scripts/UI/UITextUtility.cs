using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public static class UITextUtility
    {
        public static void SetText(Component textComponent, string value)
        {
            if (textComponent == null)
            {
                return;
            }

            if (textComponent is Text legacyText)
            {
                legacyText.text = value;
                return;
            }

            PropertyInfo textProperty = textComponent.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProperty != null && textProperty.PropertyType == typeof(string))
            {
                textProperty.SetValue(textComponent, value);
            }
        }
    }
}
