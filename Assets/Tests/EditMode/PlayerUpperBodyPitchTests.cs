using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerUpperBodyPitchTests
{
    [Test]
    public void ThirdPersonPitchUsesExpandedButFiniteLimits()
    {
        GameObject owner = new GameObject("PlayerUpperBodyPitchTest");
        try
        {
            Type controllerType = Type.GetType("PlayerAnimationCtrl, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null);
            Component controller = owner.AddComponent(controllerType);
            float lookUp = (float)controllerType.GetField(
                "_maximumLookUpAngle",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            float lookDown = (float)controllerType.GetField(
                "_maximumLookDownAngle",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);

            Assert.That(lookUp, Is.EqualTo(42f));
            Assert.That(lookDown, Is.EqualTo(32f));
            Assert.That(lookUp, Is.InRange(1f, 60f));
            Assert.That(lookDown, Is.InRange(1f, 60f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

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
