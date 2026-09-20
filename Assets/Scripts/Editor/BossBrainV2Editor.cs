using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

[CustomEditor(typeof(BossBrainV2))]
public class BossBrainV2Editor : Editor
{
    private ReorderableList attackChainList;
    private ReorderableList interruptResponsesList;
    private bool showAttackConfigs = false;
    private bool showExceptionConfigs = true;
    private bool showPhaseConfigs = true;
    private bool showArenaSettings = true;

    private static readonly Color[] attackTypeColors = new Color[]
    {
        new Color(1f, 0.3f, 0.3f),   // WingAttack
        new Color(0.8f, 0.3f, 0.8f), // DashAttack
        new Color(0.3f, 0.8f, 0.9f), // WavePushBack
        new Color(1f, 0.5f, 0.7f),   // HeadSmash
        new Color(1f, 0.8f, 0.3f),   // RangedAttack
        new Color(0.3f, 0.3f, 1f),   // PreciseProjectile
        new Color(0.3f, 0.8f, 0.3f), // UpDownAttack
        new Color(0.9f, 0.6f, 0.3f), // Chase
        new Color(0.2f, 0.9f, 0.5f), // ArenaReposition
    };

    // Constants for consistent spacing
    private const float LINE_PAD = 4f;
    private const float SECTION_PAD = 10f;
    private const float INDENT = 15f;

    private void OnEnable()
    {
        SerializedProperty chainProp = serializedObject.FindProperty("attackChain");
        if (chainProp != null)
        {
            attackChainList = CreateChainList(chainProp, "Boss Attack Chain Loop");
        }

        SerializedProperty interruptProp = serializedObject.FindProperty("hitInterruptConfig").FindPropertyRelative("interruptResponses");
        if (interruptProp != null)
        {
            interruptResponsesList = CreateChainList(interruptProp, "Interrupt Responses (Randomly Picked)");
        }
    }

    private ReorderableList CreateChainList(SerializedProperty prop, string headerTitle)
    {
        ReorderableList list = new ReorderableList(serializedObject, prop, true, true, true, true);

        list.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, headerTitle, EditorStyles.boldLabel);
        };

        list.elementHeightCallback = (int index) =>
        {
            return CalculateElementHeight(prop.GetArrayElementAtIndex(index));
        };

        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            DrawChainElement(rect, prop.GetArrayElementAtIndex(index));
        };

        list.onAddCallback = (ReorderableList l) =>
        {
            int i = l.serializedProperty.arraySize;
            l.serializedProperty.arraySize++;
            SerializedProperty el = l.serializedProperty.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("attackType").enumValueIndex = 0;
            el.FindPropertyRelative("useIdleAfter").boolValue = true;
            el.FindPropertyRelative("idleTimeMin").floatValue = 1.0f;
            el.FindPropertyRelative("idleTimeMax").floatValue = 2.0f;
            el.FindPropertyRelative("minDistance").floatValue = 0f;
            el.FindPropertyRelative("maxDistance").floatValue = 0f;
            el.FindPropertyRelative("probability").floatValue = 1.0f;
            el.FindPropertyRelative("skipIfConditionsFail").boolValue = true;
            el.FindPropertyRelative("showAdvanced").boolValue = false;
            el.isExpanded = true;
        };

        return list;
    }

    // ================================================================
    // HEIGHT CALCULATION (must match draw logic exactly)
    // ================================================================

    private float CalculateElementHeight(SerializedProperty element)
    {
        float line = EditorGUIUtility.singleLineHeight;
        if (!element.isExpanded) return line + 6f;

        float h = line + 6f; // Header row (type dropdown)
        h += SECTION_PAD;

        // "Wait (Idle) After This?" toggle
        h += line + LINE_PAD;
        if (element.FindPropertyRelative("useIdleAfter").boolValue)
            h += line + LINE_PAD; // idle min/max row

        h += SECTION_PAD;

        // "Execution Conditions" header
        h += line + LINE_PAD;
        // Distance row
        h += line + LINE_PAD;
        // Probability
        h += line + LINE_PAD;
        // Skip toggle
        h += line + LINE_PAD;

        h += SECTION_PAD;

        // Chase overrides
        BossAttackType type = (BossAttackType)element.FindPropertyRelative("attackType").enumValueIndex;
        if (type == BossAttackType.Chase)
        {
            h += line + LINE_PAD; // header
            h += (line + LINE_PAD) * 3; // 3 fields
            h += SECTION_PAD;
        }

        // Inline config box
        SerializedProperty configProp = GetConfigProperty(type);
        if (configProp != null)
        {
            bool showAdv = element.FindPropertyRelative("showAdvanced").boolValue;
            h += line + LINE_PAD; // "Global Settings" header + Advanced toggle
            h += SECTION_PAD / 2;
            h += MeasureConfigHeight(configProp, showAdv);
            h += SECTION_PAD; // bottom padding inside box
        }

        h += SECTION_PAD; // final bottom padding
        return h;
    }

    // ================================================================
    // DRAW LOGIC
    // ================================================================

    private void DrawChainElement(Rect rect, SerializedProperty element)
    {
        SerializedProperty typeProp = element.FindPropertyRelative("attackType");
        SerializedProperty showAdvancedProp = element.FindPropertyRelative("showAdvanced");

        float line = EditorGUIUtility.singleLineHeight;
        float y = rect.y + 2f;
        float contentX = rect.x;
        float contentW = rect.width;

        // Color band on the left
        int typeIndex = typeProp.enumValueIndex;
        Color bandColor = (typeIndex >= 0 && typeIndex < attackTypeColors.Length) ? attackTypeColors[typeIndex] : Color.gray;
        EditorGUI.DrawRect(new Rect(rect.x - 14, rect.y, 5, rect.height), bandColor);

        // --- HEADER ROW: Foldout + Type Dropdown ---
        element.isExpanded = EditorGUI.Foldout(new Rect(contentX + 10, y, 14, line), element.isExpanded, GUIContent.none);
        EditorGUI.PropertyField(new Rect(contentX + 28, y, contentW - 28, line), typeProp, GUIContent.none);
        y += line + 6f;

        if (!element.isExpanded) return;

        y += SECTION_PAD - 6f;

        // --- FLOW CONTROL ---
        SerializedProperty useIdleProp = element.FindPropertyRelative("useIdleAfter");
        EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), useIdleProp, new GUIContent("Wait (Idle) After This?"));
        y += line + LINE_PAD;

        if (useIdleProp.boolValue)
        {
            float labelW = 90f;
            float remaining = contentW - INDENT - labelW - 30f;
            float fieldW = remaining / 2f;
            float x = contentX + INDENT;

            EditorGUI.LabelField(new Rect(x, y, labelW, line), "Idle Duration");
            x += labelW;
            EditorGUI.PropertyField(new Rect(x, y, fieldW, line), element.FindPropertyRelative("idleTimeMin"), GUIContent.none);
            x += fieldW + 4f;
            EditorGUI.LabelField(new Rect(x, y, 20f, line), "→");
            x += 20f;
            EditorGUI.PropertyField(new Rect(x, y, fieldW, line), element.FindPropertyRelative("idleTimeMax"), GUIContent.none);
            y += line + LINE_PAD;
        }

        y += SECTION_PAD;

        // --- EXECUTION CONDITIONS ---
        EditorGUI.LabelField(new Rect(contentX, y, contentW, line), "Execution Conditions", EditorStyles.boldLabel);
        y += line + LINE_PAD;

        // Distance row
        {
            float labelW = 110f;
            float remaining = contentW - INDENT - labelW - 30f;
            float fieldW = remaining / 2f;
            float x = contentX + INDENT;

            EditorGUI.LabelField(new Rect(x, y, labelW, line), "Min Dist (0=any)");
            x += labelW;
            EditorGUI.PropertyField(new Rect(x, y, fieldW, line), element.FindPropertyRelative("minDistance"), GUIContent.none);
            x += fieldW + 4f;
            EditorGUI.LabelField(new Rect(x, y, 20f, line), "→");
            x += 20f;

            float maxLabelW = 90f;
            EditorGUI.LabelField(new Rect(x, y, maxLabelW, line), "Max (0=∞)");
            x += maxLabelW;
            EditorGUI.PropertyField(new Rect(x, y, fieldW - maxLabelW + 20f, line), element.FindPropertyRelative("maxDistance"), GUIContent.none);
        }
        y += line + LINE_PAD;

        EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), element.FindPropertyRelative("probability"), new GUIContent("Execution Probability"));
        y += line + LINE_PAD;

        EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), element.FindPropertyRelative("skipIfConditionsFail"), new GUIContent("Skip if conditions fail"));
        y += line + LINE_PAD;

        y += SECTION_PAD;

        // --- CHASE OVERRIDES (only for Chase type) ---
        BossAttackType attackType = (BossAttackType)typeProp.enumValueIndex;
        if (attackType == BossAttackType.Chase)
        {
            EditorGUI.LabelField(new Rect(contentX, y, contentW, line), "Chase Overrides", EditorStyles.boldLabel);
            y += line + LINE_PAD;

            EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), element.FindPropertyRelative("chaseSpeedOverride"), new GUIContent("Speed Override (0 = global)"));
            y += line + LINE_PAD;

            EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), element.FindPropertyRelative("chaseTimeOverride"), new GUIContent("Time Override (0 = global)"));
            y += line + LINE_PAD;

            EditorGUI.PropertyField(new Rect(contentX + INDENT, y, contentW - INDENT, line), element.FindPropertyRelative("chaseStopDistanceOverride"), new GUIContent("Stop Distance (0 = global)"));
            y += line + LINE_PAD;

            y += SECTION_PAD;
        }

        // --- INLINE GLOBAL CONFIG ---
        SerializedProperty configProp = GetConfigProperty(attackType);
        if (configProp != null)
        {
            bool showAdv = showAdvancedProp.boolValue;
            float configH = MeasureConfigHeight(configProp, showAdv);
            float boxH = configH + line + LINE_PAD + SECTION_PAD + SECTION_PAD / 2;

            // Draw box background
            Rect boxRect = new Rect(contentX - 2, y - 2, contentW + 4, boxH + 4);
            EditorGUI.DrawRect(boxRect, new Color(0, 0, 0, 0.1f));

            // Header row: Title + Advanced toggle
            EditorGUI.LabelField(new Rect(contentX + 5, y, contentW - 105, line), $"Global Settings: {attackType}", EditorStyles.boldLabel);
            showAdvancedProp.boolValue = GUI.Toggle(new Rect(contentX + contentW - 95, y, 95, line), showAdv, "▶ Advanced", EditorStyles.miniButton);
            y += line + LINE_PAD;
            y += SECTION_PAD / 2;

            // Draw each visible property
            DrawConfigInline(configProp, showAdv, contentX + 5, contentW - 10, ref y);
        }
    }

    // ================================================================
    // CONFIG PROPERTY HELPERS
    // ================================================================

    private void DrawConfigInline(SerializedProperty configProp, bool showAdvanced, float x, float w, ref float y)
    {
        SerializedProperty iter = configProp.Copy();
        SerializedProperty end = iter.GetEndProperty();
        iter.NextVisible(true);

        while (iter.NextVisible(false) && !SerializedProperty.EqualContents(iter, end))
        {
            if (!showAdvanced && IsPropertyAdvanced(iter.name)) continue;

            float h = EditorGUI.GetPropertyHeight(iter, new GUIContent(iter.displayName), true);
            EditorGUI.PropertyField(new Rect(x, y, w, h), iter, new GUIContent(iter.displayName), true);
            y += h + LINE_PAD;
        }
    }

    private float MeasureConfigHeight(SerializedProperty configProp, bool showAdvanced)
    {
        float total = 0f;
        SerializedProperty iter = configProp.Copy();
        SerializedProperty end = iter.GetEndProperty();
        iter.NextVisible(true);

        while (iter.NextVisible(false) && !SerializedProperty.EqualContents(iter, end))
        {
            if (!showAdvanced && IsPropertyAdvanced(iter.name)) continue;
            total += EditorGUI.GetPropertyHeight(iter, new GUIContent(iter.displayName), true) + LINE_PAD;
        }
        return total;
    }

    // ================================================================
    // ADVANCED PROPERTY FILTER
    // ================================================================

    private bool IsPropertyAdvanced(string name)
    {
        // --- WingAttack ---
        if (name == "fastWindupTime" || name == "activeTime") return true;

        // --- DashAttack (VFX + knockback) ---
        if (name == "chargeVFXPrefab" || name == "chargeVFXOffset" || name == "chargeVFXScale") return true;
        if (name == "dashVFXPrefab" || name == "dashVFXOffset" || name == "dashVFXScale") return true;
        if (name == "knockbackForce" || name == "knockbackStunDuration") return true;

        // --- WavePushBack (per-flap) ---
        if (name == "flapInterval") return true;
        if (name == "firstFlapForce" || name == "firstFlapStun") return true;
        if (name == "secondFlapForce" || name == "secondFlapStun") return true;
        if (name == "followUpFlapForce" || name == "followUpFlapStun") return true;

        // --- HeadSmash (shockwave + VFX + throw) ---
        if (name == "stuckTime" || name == "actionTime") return true;
        if (name == "smashKnockbackForce" || name == "smashKnockbackStun") return true;
        if (name == "throwKnockbackForce" || name == "throwKnockbackStun" || name == "throwAfterUsageCount") return true;
        if (name == "closeRangeThreshold" || name == "shockwaveExitTime") return true;
        if (name == "shockwaveSpeed" || name == "shockwaveTravelDistance" || name == "shockwaveDamage") return true;
        if (name == "groundSmashVFXPrefab" || name == "groundSmashHeightOffset" || name == "groundSmashForwardOffset" || name == "groundSmashRotationOffset") return true;

        // --- RangedAttack (ring drop + VFX) ---
        if (name == "ringDropDelay" || name == "ringDropWaitAfterFire" || name == "ringCount") return true;
        if (name == "ringRadius" || name == "ringDamage" || name == "centerShots") return true;
        if (name == "vfxYOffset" || name == "vfxScale" || name == "vfxRotationOffset" || name == "projectileBaseScale") return true;
        if (name == "shotIntervalMin" || name == "shotIntervalMax") return true;

        // --- PreciseProjectile (punish) ---
        if (name == "punishRadius" || name == "punishDamage" || name == "punishKnockbackForce" || name == "punishKnockbackStun") return true;

        // --- UpDownAttack (rock physics + VFX) ---
        if (name == "rockForce" || name == "rockSpawnRadius" || name == "rockScale") return true;
        if (name == "dropTime") return true;
        if (name == "landKnockbackForce" || name == "landKnockbackStun") return true;

        // --- Chase (physics tuning) ---
        if (name == "pushPauseDuration" || name == "runAnimThreshold" || name == "acceleration") return true;
        if (name == "pushbackForce" || name == "pushbackStun") return true;

        // --- ArenaReposition (ring drop + peak heights) ---
        if (name == "fireDelay" || name == "waitAfterFire" || name == "centerDamage") return true;
        if (name == "jumpUpPeakHeight" || name == "jumpDownPeakHeight") return true;

        // --- Shared ---
        if (name == "recoveryIdleTime") return true;

        return false;
    }

    // ================================================================
    // CONFIG PROPERTY LOOKUP
    // ================================================================

    private SerializedProperty GetConfigProperty(BossAttackType type)
    {
        switch (type)
        {
            case BossAttackType.WingAttack: return serializedObject.FindProperty("wingAttackConfig");
            case BossAttackType.DashAttack: return serializedObject.FindProperty("dashAttackConfig");
            case BossAttackType.WavePushBack: return serializedObject.FindProperty("wavePushBackConfig");
            case BossAttackType.HeadSmash: return serializedObject.FindProperty("headSmashConfig");
            case BossAttackType.RangedAttack: return serializedObject.FindProperty("rangedAttackConfig");
            case BossAttackType.PreciseProjectile: return serializedObject.FindProperty("preciseProjectileConfig");
            case BossAttackType.UpDownAttack: return serializedObject.FindProperty("upDownAttackConfig");
            case BossAttackType.Chase: return serializedObject.FindProperty("chaseConfig");
            case BossAttackType.ArenaReposition: return serializedObject.FindProperty("arenaRepositionConfig");
            default: return null;
        }
    }

    // ================================================================
    // MAIN INSPECTOR
    // ================================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("idleConfig"), true);
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("ATTACK CHAIN SYSTEM", EditorStyles.boldLabel);
        if (attackChainList != null) attackChainList.DoLayoutList();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackAttack"));

        SerializedProperty testMode = serializedObject.FindProperty("testingMode");
        if (testMode != null)
        {
            EditorGUILayout.PropertyField(testMode);
            if (testMode.boolValue)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("testingModeAttackChain"), true);
        }

        EditorGUILayout.Space(10);
        showAttackConfigs = EditorGUILayout.Foldout(showAttackConfigs, "GLOBAL TEMPLATES (Config Files)", true, EditorStyles.foldoutHeader);
        if (showAttackConfigs)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wingAttackConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dashAttackConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wavePushBackConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headSmashConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rangedAttackConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("preciseProjectileConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("upDownAttackConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("chaseConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arenaRepositionConfig"), true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        showExceptionConfigs = EditorGUILayout.Foldout(showExceptionConfigs, "INTERRUPTS & EXCEPTIONS", true, EditorStyles.foldoutHeader);
        if (showExceptionConfigs)
        {
            EditorGUI.indentLevel++;
            SerializedProperty hitInterruptProp = serializedObject.FindProperty("hitInterruptConfig");
            hitInterruptProp.isExpanded = EditorGUILayout.Foldout(hitInterruptProp.isExpanded, hitInterruptProp.displayName);
            if (hitInterruptProp.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(hitInterruptProp.FindPropertyRelative("enabled"));
                EditorGUILayout.PropertyField(hitInterruptProp.FindPropertyRelative("minHitsBeforeInterrupt"));
                EditorGUILayout.PropertyField(hitInterruptProp.FindPropertyRelative("maxHitsBeforeInterrupt"));
                EditorGUILayout.PropertyField(hitInterruptProp.FindPropertyRelative("interruptCooldown"));

                EditorGUILayout.Space(5);
                if (interruptResponsesList != null)
                {
                    int oldIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 0;
                    
                    Rect listRect = GUILayoutUtility.GetRect(0f, interruptResponsesList.GetHeight(), GUILayout.ExpandWidth(true));
                    listRect.x += 15f;
                    listRect.width -= 15f;
                    interruptResponsesList.DoList(listRect);
                    
                    EditorGUI.indentLevel = oldIndent;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("edgeWatchConfig"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("deathConfig"), true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        showPhaseConfigs = EditorGUILayout.Foldout(showPhaseConfigs, "BOSS PHASES", true, EditorStyles.foldoutHeader);
        if (showPhaseConfigs)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("phases"), true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        showArenaSettings = EditorGUILayout.Foldout(showArenaSettings, "ARENA & NAVIGATION", true, EditorStyles.foldoutHeader);
        if (showArenaSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("groundLayer"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("waypointSetupRoot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arenaWaypoints"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHopsBeforeGrapple"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
