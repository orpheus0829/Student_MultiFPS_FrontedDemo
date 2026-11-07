using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerMovementPlayModeTests
{
    [UnityTest]
    public IEnumerator SlideResumesSprintOnlyWhileSprintInputIsHeld()
    {
        Type moveType = Type.GetType("PlayerMoveCtrl, Assembly-CSharp");
        Type dataType = Type.GetType("PlayerDataSO, Assembly-CSharp");
        Type actionSetType = Type.GetType("CharacterActionSetSO, Assembly-CSharp");
        Assert.That(moveType, Is.Not.Null);
        Assert.That(dataType, Is.Not.Null);
        Assert.That(actionSetType, Is.Not.Null);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(1020f, -0.25f, 1000f);
        ground.transform.localScale = new Vector3(60f, 0.5f, 60f);

        GameObject player = new GameObject("滑铲疾跑恢复测试玩家");
        player.transform.position = new Vector3(1000f, 0f, 1000f);
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.5f;
        characterController.center = new Vector3(0f, 0.75f, 0f);
        Component move = player.AddComponent(moveType);
        ScriptableObject data = ScriptableObject.CreateInstance(dataType);
        ScriptableObject actionSet = ScriptableObject.CreateInstance(actionSetType);
        SetField(move, "_playerData", data);
        SetField(move, "_actionSet", actionSet);

        yield return null;
        for (int frame = 0; frame < 12 && !characterController.isGrounded; frame++)
        {
            yield return null;
        }
        Assert.That(characterController.isGrounded, Is.True);

        Invoke(move, "SetMoveInput", Vector2.up);
        Invoke(move, "SetSprintInput", true);
        for (int frame = 0; frame < 12; frame++)
        {
            yield return null;
        }

        Assert.That(InvokeBool(move, "TryStartSlide"), Is.True);
        Assert.That(GetBool(move, "IsSprinting"), Is.False);
        yield return new WaitForSeconds(1.2f);
        Assert.That(GetBool(move, "IsSliding"), Is.False);
        Assert.That(GetBool(move, "IsSprinting"), Is.True,
            "持续按住疾跑时，滑铲结束应恢复疾跑");

        Assert.That(InvokeBool(move, "TryStartSlide"), Is.True);
        Invoke(move, "SetSprintInput", false);
        yield return new WaitForSeconds(1.2f);
        Assert.That(GetBool(move, "IsSliding"), Is.False);
        Assert.That(GetBool(move, "IsSprinting"), Is.False,
            "滑铲中松开疾跑后，不应自动恢复疾跑");

        UnityEngine.Object.Destroy(player);
        UnityEngine.Object.Destroy(ground);
        UnityEngine.Object.Destroy(data);
        UnityEngine.Object.Destroy(actionSet);
    }

    [UnityTest]
    public IEnumerator LateralSprintCanBufferSlideThroughAirborneAimCancel()
    {
        Type moveType = Type.GetType("PlayerMoveCtrl, Assembly-CSharp");
        Type dataType = Type.GetType("PlayerDataSO, Assembly-CSharp");
        Type actionSetType = Type.GetType("CharacterActionSetSO, Assembly-CSharp");
        Assert.That(moveType, Is.Not.Null);
        Assert.That(dataType, Is.Not.Null);
        Assert.That(actionSetType, Is.Not.Null);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "测试地面";
        ground.transform.position = new Vector3(1000f, -0.25f, 1000f);
        ground.transform.localScale = new Vector3(20f, 0.5f, 20f);

        GameObject player = new GameObject("移动测试玩家");
        player.transform.position = new Vector3(1000f, 0f, 1000f);
        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = 1.5f;
        characterController.center = new Vector3(0f, 0.75f, 0f);
        Component move = player.AddComponent(moveType);
        ScriptableObject data = ScriptableObject.CreateInstance(dataType);
        ScriptableObject actionSet = ScriptableObject.CreateInstance(actionSetType);
        SetField(move, "_playerData", data);
        SetField(move, "_actionSet", actionSet);

        int slideCount = 0;
        EventInfo slideStarted = moveType.GetEvent("SlideStarted");
        Action onSlide = () => slideCount++;
        slideStarted.AddEventHandler(move, onSlide);

        yield return null;
        for (int frame = 0; frame < 12 && !characterController.isGrounded; frame++)
        {
            yield return null;
        }
        Assert.That(characterController.isGrounded, Is.True);

        Invoke(move, "SetMoveInput", Vector2.right);
        Invoke(move, "SetSprintInput", true);
        Assert.That(GetBool(move, "IsSprinting"), Is.True, "纯横移应允许疾跑");
        for (int frame = 0; frame < 12; frame++)
        {
            yield return null;
        }

        Assert.That(InvokeBool(move, "TryJumpOrVault"), Is.True);
        Invoke(move, "SetSprintInput", false);
        Assert.That(GetBool(move, "IsSprinting"), Is.False);
        Assert.That(InvokeBool(move, "TryStartSlide"), Is.True,
            "空中取消疾跑后，滑铲键应被预输入缓存接收");
        Assert.That(GetBool(move, "IsSliding"), Is.False);

        for (int frame = 0; frame < 120 && !GetBool(move, "IsSliding"); frame++)
        {
            yield return null;
        }
        Assert.That(GetBool(move, "IsSliding"), Is.True, "落地后应开始滑铲");
        Assert.That(slideCount, Is.EqualTo(1), "落地滑铲应只发送一次 SlideStarted");

        slideStarted.RemoveEventHandler(move, onSlide);
        UnityEngine.Object.Destroy(player);
        UnityEngine.Object.Destroy(ground);
        UnityEngine.Object.Destroy(data);
        UnityEngine.Object.Destroy(actionSet);
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        FieldInfo field = component.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(component, value);
    }

    private static object Invoke(Component component, string methodName, params object[] args)
    {
        MethodInfo method = component.GetType().GetMethod(methodName);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(component, args);
    }

    private static bool InvokeBool(Component component, string methodName)
    {
        return (bool)Invoke(component, methodName);
    }

    private static bool GetBool(Component component, string propertyName)
    {
        PropertyInfo property = component.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null, propertyName);
        return (bool)property.GetValue(component);
    }
}
