using UnityEngine;
using UnityEditor;

namespace PubScale.SdkOne
{
    [CustomEditor(typeof(VideoAdDisplayHandler))]
    public class VideoAdDisplayHandlerEditor : Editor
    {


        protected VideoAdDisplayHandler targetDisplayer;
        protected GUIStyle styleValid;
        protected GUIStyle styleInvalid;


        protected PubEditorUXState prevEditorGUIState = new PubEditorUXState();




        public virtual void OnEnable()
        {
            targetDisplayer = (VideoAdDisplayHandler)target;

            styleValid = new GUIStyle();
            styleValid.normal.textColor = new Color(0f, 0.5f, 0f, 1f);

            styleInvalid = new GUIStyle();
            styleInvalid.normal.textColor = new Color(0.6f, 0f, 0f, 1f);

        }


        public override void OnInspectorGUI()
        {

            //--------------------------------

            PubEditorUX.Start_CustomEditor(serializedObject, prevEditorGUIState);

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                PubEditorUX.DisplayHeading("VIDEO AD DISPLAY FIELDS");
            }

            DrawDefaultInspector();


            EditorGUILayout.Space();

            PubEditorUX.End_CustomEditor(serializedObject, prevEditorGUIState);

        }



    }
}
