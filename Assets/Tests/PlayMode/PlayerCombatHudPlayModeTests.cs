using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class PlayerCombatHudPlayModeTests
{
    [UnityTest]
    public IEnumerator HudTracksHealthAndHidesImmediatelyOnDeath()
    {
        Type hudType = RequireType("PlayerCombatHudUI");
        GameObject canvasObject = new GameObject(
            "HUD测试画布", typeof(RectTransform), typeof(Canvas));
        Component hud = canvasObject.AddComponent(hudType);

        try
        {
            yield return null;
            Invoke(hud, "SetAlive", true);
            Invoke(hud, "HandleHealthChanged", 100f, 100f);

            Assert.That(GetProperty<bool>(hud, "IsHudVisible"), Is.True);
            Assert.That(GetProperty<float>(hud, "HealthNormalized"), Is.EqualTo(1f).Within(0.001f));
            Assert.That(GetProperty<Slider>(hud, "HealthSlider"), Is.Not.Null);
            Assert.That(GetProperty<object>(hud, "MagazineText"), Is.Not.Null);
            Assert.That(GetProperty<object>(hud, "ReserveText"), Is.Not.Null);

            Invoke(hud, "HandleHealthChanged", 60f, 100f);
            yield return null;
            Assert.That(GetProperty<float>(hud, "HealthNormalized"), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(GetProperty<float>(hud, "DamageSeverity"), Is.GreaterThan(0f));

            Invoke(hud, "HandleDied");
            Assert.That(GetProperty<bool>(hud, "IsHudVisible"), Is.False,
                "死亡事件当帧必须隐藏血条、枪械弹药和屏幕受伤覆盖层");
            Assert.That(GetProperty<Image>(hud, "BloodOverlay").enabled, Is.False);
            Assert.That(GetProperty<Image>(hud, "RedGlow").enabled, Is.False);
        }
        finally
        {
            UnityEngine.Object.Destroy(canvasObject);
        }
    }

    [UnityTest]
    public IEnumerator AiCannotBeBoundAsLocalScreenHud()
    {
        Type hudType = RequireType("PlayerCombatHudUI");
        Type aiType = RequireType("AIPlayerCtrl");
        GameObject canvasObject = new GameObject(
            "HUD测试画布", typeof(RectTransform), typeof(Canvas));
        GameObject ai = new GameObject("HUD测试AI");
        ai.SetActive(false);
        ai.AddComponent(aiType);
        Component hud = canvasObject.AddComponent(hudType);

        try
        {
            hudType.GetMethod("Bind")?.Invoke(hud, new object[] { ai });
            yield return null;
            Assert.That(GetProperty<bool>(hud, "IsHudVisible"), Is.False,
                "AI 不能占用本地屏幕战斗 HUD");
        }
        finally
        {
            UnityEngine.Object.Destroy(canvasObject);
            UnityEngine.Object.Destroy(ai);
        }
    }

    private static void Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, arguments);
    }

    private static Type RequireType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "缺少运行时类型 " + typeName);
        return type;
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, "缺少属性 " + propertyName);
        return (T)property.GetValue(target);
    }
}
