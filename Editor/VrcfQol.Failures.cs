#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UmeVrcfQol
{
    internal static partial class VrcfQol
    {
        internal static bool IsToolFailure(Exception ex)
        {
            return ex is TargetInvocationException or TargetException or MemberAccessException or ArgumentException
                or InvalidCastException or InvalidOperationException or NotSupportedException
                or UnityException or MissingReferenceException;
        }

        internal static bool IsCallbackFailure(Exception ex)
        {
            return IsToolFailure(ex) || ex is NullReferenceException or IndexOutOfRangeException or KeyNotFoundException
                or RegexMatchTimeoutException;
        }

        internal static bool ReadToggleSlider(FieldInfo? sliderField, object toggle)
        {
            if (sliderField == null)
            {
                return false;
            }

            try
            {
                return (bool)sliderField.GetValue(toggle);
            }
            catch (Exception ex) when (IsToolFailure(ex))
            {
                return false;
            }
        }
    }
}
