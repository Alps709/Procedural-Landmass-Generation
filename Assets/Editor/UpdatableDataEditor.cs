﻿using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(UpdatableData), true)]
    public class UpdatableDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            UpdatableData data = (UpdatableData)target;

            if(GUILayout.Button("Update"))
            {
                data.NotifyOfUpdatedValues();
            }
        }
    }
}
