using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDefinitionSO))]
public sealed class WeaponDefinitionSOEditor : Editor
{
    private bool _showBasic = true;
    private bool _showCombat = true;
    private bool _showAccuracy = true;
    private bool _showHandling = true;
    private bool _showPresentation = true;
    private bool _showOriginalRecoil;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawTitle();
        DrawBasicSection();
        DrawCombatSection();
        DrawAccuracySection();
        DrawHandlingSection();
        DrawPresentationSection();
        DrawOriginalRecoilSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTitle()
    {
        SerializedProperty nameProperty = Find("_basic._weaponName");
        string weaponName = string.IsNullOrWhiteSpace(nameProperty.stringValue)
            ? "未命名枪械"
            : nameProperty.stringValue;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(weaponName, EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
    }

    private void DrawBasicSection()
    {
        _showBasic = EditorGUILayout.BeginFoldoutHeaderGroup(_showBasic, "基础信息");
        if (_showBasic)
        {
            Draw("_basic._weaponId", "枪械 ID");
            Draw("_basic._weaponName", "枪械名称");
            Draw("_basic._category", "枪械类别");
            Draw("_basic._tags", "枪械标签");
            Draw("_basic._iconResourceId", "武器图标");
            Draw("_basic._silhouetteResourceId", "武器剪影");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawCombatSection()
    {
        _showCombat = EditorGUILayout.BeginFoldoutHeaderGroup(_showCombat, "射击与伤害");
        if (_showCombat)
        {
            DrawFireData();
            DrawDamageData();
            DrawAmmoData();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawFireData()
    {
        DrawSubTitle("射击模式");
        SerializedProperty modesProperty = Find("_fire._allowedModes");
        DrawProperty(modesProperty, "允许的射击模式");
        Draw("_fire._defaultMode", "默认射击模式");
        Draw("_fire._canSwitchMode", "允许切换射击模式");

        WeaponFireModeMask modes = (WeaponFireModeMask)modesProperty.intValue;
        if ((modes & WeaponFireModeMask.SemiAutomatic) != 0)
        {
            Draw("_fire._semiAutomaticRpm", "半自动射速", "单位：发/分钟。");
        }

        if ((modes & WeaponFireModeMask.Automatic) != 0)
        {
            Draw("_fire._automaticRpm", "全自动射速", "单位：发/分钟。");
        }

        if ((modes & WeaponFireModeMask.Burst) != 0)
        {
            Draw("_fire._burstRpm", "连发射速", "单位：发/分钟。");
            Draw("_fire._burstCount", "每组连发数量");
            Draw("_fire._burstCooldown", "连发组间隔");
        }

        Draw("_fire._pelletsPerShot", "每次弹丸数量");
        Draw("_fire._fireDelay", "开火延迟");
        Draw("_fire._dryFireEnabled", "允许空仓击发");
    }

    private void DrawDamageData()
    {
        DrawSubTitle("伤害与距离");
        Draw("_damage._closeDamage", "近距离伤害");
        Draw("_damage._farDamage", "远距离伤害");
        Draw("_damage._closeDamageRange", "满伤害距离");
        Draw("_damage._farDamageRange", "最低伤害距离");
        Draw("_damage._maximumHitRange", "最大命中距离");
        Draw("_damage._headMultiplier", "头部倍率");
        Draw("_damage._upperBodyMultiplier", "上半身倍率");
        Draw("_damage._lowerBodyMultiplier", "下半身倍率");
        DrawSubTitle("命中与击杀得分");
        Draw("_damage._baseHitScore", "基础命中分");
        Draw("_damage._weaponScoreFactor", "武器得分系数");
        Draw("_damage._closeDistanceScoreFactor", "近距离得分系数");
        Draw("_damage._farDistanceScoreFactor", "远距离得分系数");
        Draw("_damage._killBonus", "击杀奖励");
    }

    private void DrawAmmoData()
    {
        DrawSubTitle("弹量与换弹");
        Draw("_ammo._magazineCapacity", "弹匣容量");
        Draw("_ammo._startingReserveAmmo", "初始备弹");
        Draw("_ammo._maximumReserveAmmo", "最大备弹");
        SerializedProperty styleProperty = Find("_ammo._reloadStyle");
        DrawProperty(styleProperty, "换弹方式");
        Draw("_ammo._tacticalReloadDuration", "非空仓换弹时间");
        Draw("_ammo._emptyReloadDuration", "空仓换弹时间");
        Draw("_ammo._ammoCommitTime", "完成补弹时间点");

        WeaponReloadStyle style = (WeaponReloadStyle)styleProperty.enumValueIndex;
        if (style == WeaponReloadStyle.PerRound || style == WeaponReloadStyle.Revolver)
        {
            Draw("_ammo._perRoundInsertDuration", "每发装填时间");
        }

        Draw("_ammo._canInterruptReload", "允许取消换弹");
        Draw("_ammo._autoReloadWhenEmpty", "空仓自动换弹");
        Draw("_ammo._originalAmmoResetTimeScale", "原资源补弹时间倍率");
    }

    private void DrawAccuracySection()
    {
        _showAccuracy = EditorGUILayout.BeginFoldoutHeaderGroup(_showAccuracy, "散布与后坐力");
        if (_showAccuracy)
        {
            DrawSubTitle("散布");
            Draw("_accuracy._hipSpread", "腰射散布");
            Draw("_accuracy._aimSpread", "瞄准散布");
            Draw("_accuracy._maximumSpread", "最大散布");
            Draw("_accuracy._spreadPerShot", "每发散布增加");
            Draw("_accuracy._spreadRecoveryDelay", "散布恢复延迟");
            Draw("_accuracy._spreadRecoverySpeed", "散布恢复速度");
            Draw("_accuracy._movingSpreadMultiplier", "移动散布倍率");
            Draw("_accuracy._sprintingSpreadMultiplier", "冲刺散布倍率");
            Draw("_accuracy._airborneSpreadMultiplier", "空中散布倍率");
            Draw("_accuracy._crouchingSpreadMultiplier", "蹲姿散布倍率");
            Draw("_accuracy._firstShotAccurate", "首发精准");

            if (Find("_fire._pelletsPerShot").intValue > 1)
            {
                Draw("_accuracy._pelletSpread", "霰弹弹丸散布");
            }

            DrawSubTitle("游戏后坐力");
            Draw("_gameplayRecoil._verticalRecoil", "垂直后坐范围");
            Draw("_gameplayRecoil._horizontalRecoil", "水平后坐范围");
            Draw("_gameplayRecoil._randomness", "后坐随机程度");
            Draw("_gameplayRecoil._firstShotMultiplier", "首发后坐倍率");
            Draw("_gameplayRecoil._aimMultiplier", "瞄准后坐倍率");
            Draw("_gameplayRecoil._crouchMultiplier", "蹲姿后坐倍率");
            Draw("_gameplayRecoil._recoveryDelay", "后坐恢复延迟");
            Draw("_gameplayRecoil._recoverySpeed", "后坐恢复速度");
            DrawVector2List(Find("_gameplayRecoil._pattern"), "后坐趋势点");
            Draw("_gameplayRecoil._patternResetTime", "趋势重置时间");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawHandlingSection()
    {
        _showHandling = EditorGUILayout.BeginFoldoutHeaderGroup(_showHandling, "瞄准与机动");
        if (_showHandling)
        {
            SerializedProperty canAimProperty = Find("_handling._canAim");
            DrawProperty(canAimProperty, "允许瞄准");
            if (canAimProperty.boolValue)
            {
                Draw("_handling._aimFov", "瞄准视野");
                Draw("_handling._aimInDuration", "进入瞄准时间");
                Draw("_handling._aimOutDuration", "退出瞄准时间");
                Draw("_handling._aimSensitivityMultiplier", "瞄准灵敏度倍率");
                Draw("_handling._aimMovementSpeedMultiplier", "瞄准移动速度倍率");
                Draw("_handling._aimToFireDuration", "瞄准转开火时间");
            }

            Draw("_handling._movementSpeedMultiplier", "持枪移动速度倍率");
            Draw("_handling._equipDuration", "装备时间");
            Draw("_handling._unequipDuration", "收枪时间");
            Draw("_handling._sprintToFireDuration", "冲刺转开火时间");
            Draw("_handling._canFireWhileSprinting", "允许冲刺开火");
            Draw("_handling._canFireWhileAirborne", "允许空中开火");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPresentationSection()
    {
        _showPresentation = EditorGUILayout.BeginFoldoutHeaderGroup(_showPresentation, "模型与表现");
        if (_showPresentation)
        {
            DrawResourceData();
            DrawAnimationData();
            DrawIkData();
            DrawCameraData();
            DrawAudioData();
            DrawEffectData();
            DrawSpecialData();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawResourceData()
    {
        DrawSubTitle("固定模型");
        Draw("_resources._firstPersonPrefabResourceId", "第一人称预制体");
        Draw("_resources._worldModelPrefabResourceId", "场景展示模型");
    }

    private void DrawAnimationData()
    {
        DrawSubTitle("双视角动画");
        Draw("_animation._characterAnimatorControllerResourceId", "第一人称手臂控制器");
        Draw("_animation._weaponAnimatorControllerResourceId", "第一人称武器控制器");
        Draw("_animation._thirdPersonAnimatorControllerResourceId", "第三人称身体控制器");
        Draw("_animation._thirdPersonWeaponAnimatorControllerResourceId", "第三人称武器控制器");
        Draw("_animation._holdStyle", "持枪姿势类型");

        DrawSubTitle("动画能力");
        Draw("_animation._canInspect", "支持检视");
        Draw("_animation._hasFireAnimation", "具有角色开火动画");
        Draw("_animation._hasLastRoundFireAnimation", "具有最后一发动画");
        Draw("_animation._hasTacticalReloadAnimation", "具有非空仓换弹动画");
        Draw("_animation._hasEmptyReloadAnimation", "具有空仓换弹动画");

        WeaponReloadStyle style = (WeaponReloadStyle)Find("_ammo._reloadStyle").enumValueIndex;
        if (style == WeaponReloadStyle.PerRound || style == WeaponReloadStyle.Revolver)
        {
            Draw("_animation._hasPerRoundReloadAnimation", "具有逐发装填动画");
        }

        Draw("_animation._hasEquipOverrideAnimation", "具有特殊装备动画");
        if (Find("_fire._canSwitchMode").boolValue)
        {
            Draw("_animation._hasFireModeSwitchAnimation", "具有模式切换动画");
        }
        Draw("_animation._hasManualCycleAnimation", "具有拉栓或泵动动画");
        Draw("_animation._usesSprintTriggerDiscipline", "冲刺使用扳机纪律姿势");
    }

    private void DrawIkData()
    {
        DrawSubTitle("持枪姿势与节点");
        Draw("_ik._weaponRotationOffset", "武器旋转偏移");
        Draw("_ik._weaponPositionOffset", "武器位置偏移");
        Draw("_ik._leftClavicleOffset", "左锁骨偏移");
        Draw("_ik._rightClavicleOffset", "右锁骨偏移");
        Draw("_ik._aimPointOffset", "瞄准点偏移");
        Draw("_ik._sprintRightHandRotationOffset", "冲刺右手旋转偏移");
        Draw("_ik._adsBlend", "瞄准姿势混合");
        Draw("_ik._aimPointPath", "瞄准点节点路径");
        Draw("_ik._muzzlePointPath", "枪口节点路径");
        Draw("_ik._shellEjectionPointPath", "抛壳节点路径");
        Draw("_ik._magazinePointPath", "弹匣节点路径");
        Draw("_ik._leftHandIkPointPath", "左手 IK 节点路径");
    }

    private void DrawCameraData()
    {
        DrawSubTitle("相机开火反馈");
        DrawVectorCurve("_cameraFeedback._sourceCurve", "原资源抖动曲线");
        Draw("_cameraFeedback._sourcePitch", "原资源俯仰抖动");
        Draw("_cameraFeedback._sourceYaw", "原资源水平抖动");
        Draw("_cameraFeedback._sourceRoll", "原资源侧倾抖动");
        Draw("_cameraFeedback._sourceSmoothSpeed", "原资源平滑速度");
        Draw("_cameraFeedback._sourcePlayRate", "原资源播放速度");
        Draw("_cameraFeedback._cameraKickRotation", "相机旋转冲击");
        Draw("_cameraFeedback._cameraKickPosition", "相机位置冲击");
        Draw("_cameraFeedback._cameraKickDuration", "冲击时间");
        Draw("_cameraFeedback._cameraReturnDuration", "回正时间");
        Draw("_cameraFeedback._aimCameraKickMultiplier", "瞄准冲击倍率");
    }

    private void DrawAudioData()
    {
        DrawSubTitle("声音");
        DrawResourceIdList(Find("_audio._fireSoundResourceIds"), "开火声音", typeof(AudioClip));
        Draw("_audio._firePitchRange", "开火音高范围");
        Draw("_audio._fireVolumeRange", "开火音量范围");
        DrawResourceIdList(Find("_audio._weaponEventSoundResourceIds"), "原资源动画事件声音", typeof(AudioClip));
        Draw("_audio._dryFireSoundResourceId", "空仓击发声音");
        Draw("_audio._equipSoundResourceId", "装备声音");
        Draw("_audio._unequipSoundResourceId", "收枪声音");
        DrawResourceIdList(Find("_audio._reloadSoundResourceIds"), "换弹声音", typeof(AudioClip));
        DrawResourceIdList(Find("_audio._mechanicalSoundResourceIds"), "机械动作声音", typeof(AudioClip));
    }

    private void DrawEffectData()
    {
        DrawSubTitle("特效");
        Draw("_effects._muzzleFlashResourceId", "枪口火焰");
        Draw("_effects._muzzleSmokeResourceId", "枪口烟雾");
        Draw("_effects._shellResourceId", "弹壳预制体");
        Draw("_effects._tracerResourceId", "曳光效果");
        Draw("_effects._impactEffectResourceId", "命中特效");
    }

    private void DrawSpecialData()
    {
        DrawSubTitle("特殊表现");
        SerializedProperty typeProperty = Find("_special._specialType");
        DrawProperty(typeProperty, "特殊表现类型");
        WeaponSpecialType specialType = (WeaponSpecialType)typeProperty.enumValueIndex;
        if (specialType == WeaponSpecialType.AmmoBelt)
        {
            Draw("_special._beltRoundCount", "可见弹链数量");
            Draw("_special._beltResetTime", "弹链重置时间");
            DrawStringList(Find("_special._beltNodePaths"), "弹链节点路径");
        }
        else if (specialType == WeaponSpecialType.Pdw90Mechanism)
        {
            Draw("_special._specialAnimationResourceId", "弹匣表现动画");
            Draw("_special._specialCurve", "机械结构运动曲线");
        }
    }

    private void DrawOriginalRecoilSection()
    {
        _showOriginalRecoil = EditorGUILayout.BeginFoldoutHeaderGroup(_showOriginalRecoil, "原资源视觉后坐力");
        if (_showOriginalRecoil)
        {
            DrawSubTitle("旋转与位移");
            Draw("_originalRecoil._pitch", "俯仰范围");
            Draw("_originalRecoil._roll", "侧倾范围");
            Draw("_originalRecoil._yaw", "水平旋转范围");
            Draw("_originalRecoil._kickback", "后坐位移");
            Draw("_originalRecoil._kickUp", "上抬位移");
            Draw("_originalRecoil._kickRight", "横向位移");
            Draw("_originalRecoil._aimRotationMultiplier", "瞄准旋转倍率");
            Draw("_originalRecoil._aimPositionMultiplier", "瞄准位移倍率");

            DrawSubTitle("连射平滑");
            Draw("_originalRecoil._smoothRotation", "旋转平滑");
            Draw("_originalRecoil._smoothPosition", "位移平滑");
            Draw("_originalRecoil._extraRotation", "额外旋转");
            Draw("_originalRecoil._extraPosition", "额外位移");

            DrawSubTitle("噪声与推枪");
            Draw("_originalRecoil._noiseX", "水平噪声");
            Draw("_originalRecoil._noiseY", "垂直噪声");
            Draw("_originalRecoil._noiseAcceleration", "噪声加速度");
            Draw("_originalRecoil._noiseDamping", "噪声阻尼");
            Draw("_originalRecoil._noiseScale", "噪声强度");
            Draw("_originalRecoil._pushAmount", "推枪幅度");
            Draw("_originalRecoil._pushAcceleration", "推枪加速度");
            Draw("_originalRecoil._pushDamping", "推枪阻尼");

            DrawSubTitle("后坐摆动");
            Draw("_originalRecoil._sway._pitch", "俯仰摆动");
            Draw("_originalRecoil._sway._yaw", "水平摆动");
            Draw("_originalRecoil._sway._rollMultiplier", "侧倾倍率");
            Draw("_originalRecoil._sway._damping", "摆动阻尼");
            Draw("_originalRecoil._sway._acceleration", "摆动加速度");
            Draw("_originalRecoil._sway._aimScale", "瞄准摆动倍率");
            Draw("_originalRecoil._sway._pivotOffset", "摆动枢轴偏移");

            DrawSubTitle("连射累积");
            DrawProgression("_originalRecoil._pitchProgression", "俯仰累积");
            DrawProgression("_originalRecoil._upProgression", "上抬累积");
            Draw("_originalRecoil._aimProgressAlpha", "瞄准累积比例");

            DrawSubTitle("视角后坐");
            Draw("_originalRecoil._horizontalControllerRecoil", "水平视角后坐");
            Draw("_originalRecoil._verticalControllerRecoil", "垂直视角后坐");
            Draw("_originalRecoil._horizontalSmoothing", "水平视角平滑");
            Draw("_originalRecoil._verticalSmoothing", "垂直视角平滑");
            Draw("_originalRecoil._controllerDamping", "视角阻尼");

            DrawSubTitle("枢轴与播放");
            Draw("_originalRecoil._hipPivotOffset", "腰射枢轴偏移");
            Draw("_originalRecoil._aimPivotOffset", "瞄准枢轴偏移");
            Draw("_originalRecoil._smoothRoll", "平滑侧倾");
            Draw("_originalRecoil._playRate", "播放速度");

            DrawSubTitle("后坐曲线");
            DrawVectorCurve("_originalRecoil._semiRotationCurve", "半自动旋转曲线");
            DrawVectorCurve("_originalRecoil._semiPositionCurve", "半自动位移曲线");
            DrawVectorCurve("_originalRecoil._automaticRotationCurve", "全自动旋转曲线");
            DrawVectorCurve("_originalRecoil._automaticPositionCurve", "全自动位移曲线");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawProgression(string path, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        Draw(path + "._acceleration", "加速度");
        Draw(path + "._damping", "阻尼");
        Draw(path + "._amount", "幅度");
        EditorGUI.indentLevel--;
    }

    private void DrawVectorCurve(string path, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        Draw(path + "._x", "X 轴");
        Draw(path + "._y", "Y 轴");
        Draw(path + "._z", "Z 轴");
        EditorGUI.indentLevel--;
    }

    private void DrawVector2List(SerializedProperty property, string label)
    {
        DrawListHeader(property, label);
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(item, new GUIContent("第 " + (i + 1) + " 点"));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawObjectList(SerializedProperty property, string label)
    {
        DrawListHeader(property, label);
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(item, new GUIContent("第 " + (i + 1) + " 个"));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawResourceIdList(SerializedProperty property, string label, System.Type resourceType)
    {
        DrawListHeader(property, label);
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            ResourceIdDrawer.DrawLayout(
                item,
                new GUIContent("第 " + (i + 1) + " 个"),
                resourceType);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawStringList(SerializedProperty property, string label)
    {
        DrawListHeader(property, label);
        EditorGUI.indentLevel++;
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty item = property.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(item, new GUIContent("第 " + (i + 1) + " 个节点"));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawListHeader(SerializedProperty property, string label)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("添加", GUILayout.Width(48f)))
        {
            property.arraySize++;
        }
        if (GUILayout.Button("删除末项", GUILayout.Width(72f)) && property.arraySize > 0)
        {
            property.arraySize--;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSubTitle(string label)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private void Draw(string path, string label, string tooltip = "")
    {
        DrawProperty(Find(path), label, tooltip);
    }

    private void DrawProperty(SerializedProperty property, string label, string tooltip = "")
    {
        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
    }

    private SerializedProperty Find(string path)
    {
        return serializedObject.FindProperty(path);
    }
}
