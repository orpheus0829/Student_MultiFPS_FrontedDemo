using System;
using System.Reflection;
using NUnit.Framework;

public sealed class PlayerUpperBodyPitchTests
{
    [TestCase("Fire", true)]
    [TestCase("Reload", true)]
    [TestCase("EmptyReload", true)]
    [TestCase("PerRoundStart", true)]
    [TestCase("PerRoundInsert", true)]
    [TestCase("PerRoundEnd", true)]
    [TestCase("Equip", false)]
    [TestCase("Holster", false)]
    [TestCase("Inspect", false)]
    public void StationaryPitchAllowsOnlyFireAndReloadActions(
        string actionName,
        bool expected)
    {
        Type controllerType = Type.GetType("PlayerAnimationCtrl, Assembly-CSharp");
        Type actionType = Type.GetType("WeaponActionType, Assembly-CSharp");
        Assert.That(controllerType, Is.Not.Null);
        Assert.That(actionType, Is.Not.Null);
        MethodInfo method = controllerType.GetMethod(
            "AllowsStationaryUpperBodyPitch",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);
        object action = Enum.Parse(actionType, actionName);
        Assert.That(method.Invoke(null, new[] { action }), Is.EqualTo(expected));
    }
}
