using System.Reflection;

namespace FlatTree;

/// <summary>
/// Build-time validation for the delegates a node holds. Public so a custom node authored outside
/// this assembly enforces the same rule its built-in counterparts do.
/// </summary>
public static class BtGuard
{
    /// <summary>
    /// Throws when <paramref name="action"/> captures state. One node instance is shared by every
    /// agent, so a capturing delegate leaks one agent's state to all of them. Build-time only —
    /// never call this on the tick path.
    /// </summary>
    public static void RequireNoCapture(Delegate action, string paramName)
    {
        ArgumentNullException.ThrowIfNull(action, paramName);

        // A multicast delegate reports only its last entry's Target, hiding an earlier capture.
        if (action.GetInvocationList().Length > 1)
        {
            throw new ArgumentException(
                "Leaf delegates must be a single delegate, not a multicast chain.",
                paramName
            );
        }

        var target = action.Target;
        if (target is null)
        {
            return;
        }

        if (HasInstanceState(target.GetType()))
        {
            throw new ArgumentException(
                "Leaf delegates must capture nothing (use a 'static' lambda or a method group). "
                    + "One node instance is shared by every agent, so a capturing delegate leaks "
                    + "that agent's state to all others.",
                paramName
            );
        }
    }

    // A null Target is not the test: Roslyn gives non-capturing lambdas a target too. Only a
    // capturing closure's display class declares instance fields.
    private static bool HasInstanceState(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? type
    )
    {
        // GetFields does not return private fields declared on base types.
        for (; type is not null && type != typeof(object); type = type.BaseType)
        {
            var declared = type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            );

            if (declared.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
