using UnityEditor;
using UnityEngine;

namespace Ilumisoft.GameActionSystem.Editor
{
    public static class GameActionGizmos
    {
        static readonly Vector3[] vertices = new Vector3[3];

        [InitializeOnLoadMethod()]
        static void Initialize()
        {
            GameActionEditor.DrawGizmoAction = DrawConnections;
            GameActionTriggerEditor.DrawGizmoAction = DrawConnections;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Pickable | GizmoType.NotInSelectionHierarchy)]
        static void DrawConnectionGizmo(GameActionTrigger gameActionTrigger, GizmoType gizmoType)
        {
            // Cancel if the trigger has been destroyed
            if (gameActionTrigger.GameActions == null)
            {
                return;
            }

            // Set handle color to semi transparent
            Handles.color = new Color(1, 1, 1, 0.2f);

            // Draw connections
            foreach (var gameAction in gameActionTrigger.GameActions)
            {
                if (gameAction != null)
                {
                    DrawConnection(gameActionTrigger, gameAction, false);
                }
            }
        }

        /// <summary>
        /// Draws the connections from the given trigger
        /// </summary>
        /// <param name="gameActionTrigger"></param>
        public static void DrawConnections(GameActionTrigger gameActionTrigger)
        {
            foreach (var gameAction in gameActionTrigger.GameActions)
            {
                if (gameAction != null)
                {
                    DrawConnection(gameActionTrigger, gameAction);
                }
            }
        }

        /// <summary>
        /// Draws the connection between the given trigger and the given game action
        /// </summary>
        /// <param name="gameActionTrigger"></param>
        /// <param name="gameAction"></param>
        static void DrawConnection(GameActionTrigger gameActionTrigger, GameAction gameAction, bool solid = true)
        {
            var start = gameActionTrigger.transform.position;
            var end = gameAction.transform.position;
            var distance = end - start;
            var direction = distance.normalized;
            int triangleCount = Mathf.FloorToInt(distance.magnitude)+1;

            // Draw line
            if (solid)
            {
                Handles.DrawAAPolyLine(start, end);
            }
            else
            {
                // Draw dotted line
                for (int i = 0; i < triangleCount; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        var lineStart = start + (i + 0.1f) * direction;
                        var lineEnd = start + (i + 0.9f) * direction;

                        var fragmentStart = lineStart + j * (lineEnd - lineStart) / 4;
                        var fragmentEnd = fragmentStart + 0.1f * direction;

                        // Do not overshoot
                        if ((fragmentEnd - start).sqrMagnitude > distance.sqrMagnitude)
                        {
                            continue;
                        }
                        
                        Handles.DrawAAPolyLine(fragmentStart, fragmentEnd);
                    }
                }
            }

            // Draw triangles to indicate the direction of the connection
            for (var i = 1; i < triangleCount; i++)
            {
                DrawTriangle(start + i * direction, direction, 0.1f);
            }

            // Draw caps at the start and end of the connection
            Handles.DrawSolidDisc(start, Vector3.up, 0.04f);
            Handles.DrawSolidDisc(end, Vector3.up, 0.04f);
        }

        static void DrawTriangle(Vector3 position, Vector3 direction, float scale)
        {
            // Create a rotation matrix
            var rotation = Quaternion.LookRotation(direction);

            // Set the vertices of the triangle
            vertices[0] = 0.4f * scale * Vector3.left - 1.0f * scale * Vector3.forward;
            vertices[1] = Vector3.zero;
            vertices[2] = 0.4f * scale * Vector3.right - 1.0f * scale * Vector3.forward;

            // Rotate the triangle and move it to the proper position
            for (int j = 0; j < vertices.Length; j++)
            {
                vertices[j] = rotation * vertices[j] + position;
            }

            // Draw the triangle
            Handles.DrawAAConvexPolygon(vertices);
        }
    }
}