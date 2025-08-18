using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Softbody))]
public class SoftbodyEditor : Editor
{
    int selectedIndex = -1;
    bool showInstructionBox = false;
    bool editSoftbody = false;

    private void OnSceneGUI()
    {
        if (showInstructionBox) ShowInstructionBox();
        ShowClickablePointCirclesAndHandleAddRemoveSprings();
        ShowSpringLines();

        if (editSoftbody)
        {
            HandleMoveSelectedPoint();
            HandleAddPoint();
            HandleDeleteSelectedPoint();
            HandlePinPoint();
        }
    }

    public override void OnInspectorGUI()
    {
        // Show normal inspector GUI, with rest of this method adding onto the end
        DrawDefaultInspector();

        // Check for any interaction with inspector GUI element defined after this point
        EditorGUI.BeginChangeCheck();

        // Show header
        EditorGUILayout.Space();
        GUILayout.Label("Softbody Editing", EditorStyles.boldLabel);

        ShowEditButton();

        // Show checkbox for toggling the visibility of the instruction box (only enabled if editing softbody)
        EditorGUI.BeginDisabledGroup(!editSoftbody);
        showInstructionBox = EditorGUILayout.Toggle("Show Help", showInstructionBox);
        EditorGUI.EndDisabledGroup();

        // If inspector GUI element was interacted with, force scene view to refresh so that changes appear instantly instead of only when mouse hovers over scene view
        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    void ShowEditButton()
    {
        GUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel("Edit Softbody");

        // Set up edit button icon and tooltip
        GUIContent icon = EditorGUIUtility.IconContent("EditCollider");
        icon.tooltip = "Edit softbody points and springs";

        // Show edit button for toggling whether the softbody can be edited
        if (GUILayout.Toggle(editSoftbody, icon, GUI.skin.button, GUILayout.Width(35), GUILayout.Height(20)) != editSoftbody)
        {
            editSoftbody = !editSoftbody;
            selectedIndex = -1; // Ensure when editing mode is disabled that no point is selected
            showInstructionBox = false; // Ensure when editing mode is disabled that the the instruction box is also disabled
        }

        GUILayout.EndHorizontal();
    }

    void ShowInstructionBox()
    {
        float boxWidth = 450f;
        float boxHeight = 150f;
        float offsetFromRight = 20f;
        float offsetFromBottom = 40f;

        float sceneViewWidth = SceneView.lastActiveSceneView.position.width;
        float sceneViewHeight = SceneView.lastActiveSceneView.position.height;

        Rect boxRect = new Rect(
            sceneViewWidth - offsetFromRight - boxWidth,
            sceneViewHeight - offsetFromBottom - boxHeight,
            boxWidth,
            boxHeight
        );

        Handles.BeginGUI();

        GUILayout.BeginArea(boxRect, GUI.skin.box);
        GUILayout.Label("Softbody Editor Controls", EditorStyles.boldLabel);
        GUILayout.Label("Add point -> Shift + Left Click");
        GUILayout.Label("Select point -> Left Click");
        GUILayout.Label("Delete point -> Delete key while point is selected");
        GUILayout.Label("Move point -> Select point and use the move handle that appears");
        GUILayout.Label("Add spring -> Select first point and Ctrl + Left Click on second point");
        GUILayout.Label("Delete spring -> Select first point and Ctrl + Left Click on second point");
        GUILayout.Label("Pin point -> P key while point is selected");
        GUILayout.EndArea();

        Handles.EndGUI();
    }

    void ShowClickablePointCirclesAndHandleAddRemoveSprings()
    {
        Softbody softbody = target as Softbody;

        Event e = Event.current;

        for (int i = 0; i < softbody.points.Length; i++)
        {
            Point point = softbody.points[i];

            Handles.color = (i == selectedIndex) ? Color.yellow : (point.isPinned ? Color.cyan : Color.green);
            float size = HandleUtility.GetHandleSize(point.position) * 0.3f;

            // Draw circle around point (with different colour from the rest if selected), and handle if circle is clicked
            if (Handles.Button(point.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
            {
                if (!editSoftbody) return;

                // If ctrl-click on point other than currently selected one
                if (e.control && selectedIndex != -1 && selectedIndex != i)
                {
                    // If spring already exists between the two points, delete it
                    if (SpringExistsBetweenPoints(softbody, selectedIndex, i, out int springIndex))
                    {
                        // Allow removing spring to be undone
                        Undo.RecordObject(softbody, "Delete Softbody Spring");

                        // Delete spring in array at springIndex
                        softbody.springs = softbody.springs.Where((value, index) => index != springIndex).ToArray();
                    }
                    // If spring doesn't exist between the two points, create it
                    else
                    {
                        // Allow adding spring to be undone
                        Undo.RecordObject(softbody, "Add Softbody Spring");

                        // Create spring connection between currently selected point and ctrl-clicked point
                        Spring spring = new Spring() { point1 = selectedIndex, point2 = i }; // restingLength is set by softbody when simualtion starts, so no need to set it here
                        softbody.springs = softbody.springs.Append(spring).ToArray();
                    }
                }
                else
                {
                    // Make the clicked point be selected
                    selectedIndex = i;
                }
            }
        }
    }

    void ShowSpringLines()
    {
        Softbody softbody = target as Softbody;

        Handles.color = Color.green;

        foreach (Spring spring in softbody.springs)
        {
            Vector2 point1 = softbody.points[spring.point1].position;
            Vector2 point2 = softbody.points[spring.point2].position;
            Handles.DrawLine(point1, point2);
        }
    }

    void HandleMoveSelectedPoint()
    {
        Softbody softbody = target as Softbody;

        // Check if a point has been selected
        if (selectedIndex != -1)
        {
            Point point = softbody.points[selectedIndex];

            // Check for position handle change
            EditorGUI.BeginChangeCheck();

            // Show position handle and get its value
            Vector2 newPos = Handles.PositionHandle(point.position, Quaternion.identity);

            // Check position handle was moved
            if (EditorGUI.EndChangeCheck())
            {
                // Allow moving point to be undone
                Undo.RecordObject(softbody, "Move Softbody Point");

                // Set point's position to new value
                point.position = newPos;
                softbody.points[selectedIndex] = point;
            }
        }
    }

    void HandleAddPoint()
    {
        Softbody softbody = target as Softbody;

        Event e = Event.current;

        // Check if left mouse button clicked while holding down shift
        if (e.shift && e.type == EventType.MouseDown && e.button == 0)
        {
            // Get world point at where mouse click occurred
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector2 worldPos = ray.origin;

            // Allow adding point to be undone
            Undo.RecordObject(softbody, "Add Softbody Point");

            // Create new softbody point at this position
            Point newPoint = new Point() { position = worldPos, velocity = Vector2.zero };
            softbody.points = softbody.points.Append(newPoint).ToArray();

            // Make the new point be selected
            selectedIndex = softbody.points.Length - 1;

            // Prevent the mouse click from being registered by anything else
            e.Use();
        }
    }

    void HandleDeleteSelectedPoint()
    {
        Softbody softbody = target as Softbody;

        Event e = Event.current;

        // Check if delete key is pressed while a point is selected
        if (selectedIndex != -1 && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
        {
            // Allow deleting point to be undone
            Undo.RecordObject(softbody, "Delete Softbody Point");

            // Delete all springs attached to the selected point
            softbody.springs = softbody.springs.Where(spring => spring.point1 != selectedIndex && spring.point2 != selectedIndex).ToArray();

            // Delete selected point from points array
            softbody.points = softbody.points.Where((value, index) => index != selectedIndex).ToArray();

            // Shift all point indices in all springs back by one if they came after the deleted point in point array (since these points have all shifted back by one in the points array)
            for (int i = 0; i < softbody.springs.Length; i++)
            {
                Spring spring = softbody.springs[i];

                // If either spring point came after deleted point in point array, decrement the index and update the spring in the array
                if (spring.point1 > selectedIndex)
                {
                    spring.point1--;
                    softbody.springs[i] = spring;
                }
                if (spring.point2 > selectedIndex)
                {
                    spring.point2--;
                    softbody.springs[i] = spring;
                }
            }

            // Since selected point is now deleted, clear point selection
            selectedIndex = -1;

            // Prevent the delete from being registered by anything else (such as deleting the entire game object currently selected)
            e.Use();
        }
    }

    void HandlePinPoint()
    {
        Softbody softbody = target as Softbody;

        Event e = Event.current;

        // Check if P key is pressed while a point is selected
        if (selectedIndex != -1 && e.type == EventType.KeyDown && e.keyCode == KeyCode.P)
        {
            // Allow pinning/unpinning point to be undone
            Undo.RecordObject(softbody, "Toggle Pin Softbody Point");

            // Toggle point pinned status
            Point selectedPoint = softbody.points[selectedIndex];
            selectedPoint.isPinned = !selectedPoint.isPinned;
            softbody.points[selectedIndex] = selectedPoint;

            // Prevent Unity from handling the P key press
            e.Use();
        }
    }

    bool SpringExistsBetweenPoints(Softbody softbody, int point1Index, int point2Index, out int springIndex)
    {
        for (int i = 0; i < softbody.springs.Length; i++)
        {
            Spring spring = softbody.springs[i];

            bool point1Matches = spring.point1 == point1Index || spring.point1 == point2Index;
            bool point2Matches = spring.point2 == point1Index || spring.point2 == point2Index;

            if (point1Matches && point2Matches)
            {
                springIndex = i;
                return true;
            }
        }

        // If we reach here, then no spring was found
        springIndex = -1;
        return false;
    }
}
