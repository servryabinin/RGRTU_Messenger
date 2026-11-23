using System;
using System.ComponentModel;
using System.Reflection;

namespace RGRTU.Messenger.Shared.Extensions
{
    /// <summary>
    /// Расширения для перечислений: получение русского описания из атрибута [Description].
    /// </summary>
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            if (field == null)
                return value.ToString();

            DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute == null ? value.ToString() : attribute.Description;
        }
    }
}
