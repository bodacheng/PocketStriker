using System.Collections.Generic;
using UnityEngine;

namespace MCombat.Shared.Camera
{
    public abstract class CameraModeCore
    {
        protected Transform meCenter;
        protected Transform target;
        public List<Transform> myTeamTargets;
        public List<Transform> targets;

        // These values are reused by existing camera modes with mode-specific meanings.
        protected bool auto = true;
        protected float speed;
        protected float XZDis, YDis;
        protected float XZrosOffset, YrosOffset;
        public float duration, fieldOfView;

        public void SetMeCenter(Transform meCenter)
        {
            this.meCenter = meCenter;
        }

        public virtual void Enter(UnityEngine.Camera camera)
        {
        }

        public virtual void Exit(UnityEngine.Camera camera)
        {
        }

        public virtual void LocalUpdate(UnityEngine.Camera camera)
        {
        }

        public static Vector3 GetDirection(Vector3 original, float offsetAngle, float verticalAngle)
        {
            var offsetRot = Quaternion.AngleAxis(offsetAngle, Vector3.up);
            var verticalRot = Quaternion.AngleAxis(verticalAngle, Vector3.left);
            return (verticalRot * offsetRot * original).normalized;
        }

        protected Vector3 GetVerticalDir(Vector3 dir)
        {
            dir.y = 0;
            return Quaternion.AngleAxis(90, Vector3.up) * dir;
        }

        protected bool TryGetAveragePosition(IList<Transform> targetList, out Vector3 averagePosition)
        {
            averagePosition = Vector3.zero;
            if (targetList == null)
            {
                return false;
            }

            var validCount = 0;
            for (var i = 0; i < targetList.Count; i++)
            {
                var oneTarget = targetList[i];
                if (oneTarget == null)
                {
                    continue;
                }

                averagePosition += oneTarget.position;
                validCount++;
            }

            if (validCount == 0)
            {
                return false;
            }

            averagePosition /= validCount;
            return true;
        }

        protected Vector3 GetDesiredOrbitDirection(Vector3 mePosition, Vector3 enemyPosition, Vector3 currentOrbit)
        {
            var combatLine = enemyPosition - mePosition;
            combatLine.y = 0;
            if (combatLine.sqrMagnitude <= 0.001f)
            {
                currentOrbit.y = 0;
                return currentOrbit.sqrMagnitude > 0.001f ? currentOrbit.normalized : -Vector3.forward;
            }

            var desiredOrbit = Quaternion.AngleAxis(90f, Vector3.up) * combatLine.normalized;
            currentOrbit.y = 0;
            if (currentOrbit.sqrMagnitude > 0.001f && Vector3.Dot(desiredOrbit, currentOrbit.normalized) < 0f)
            {
                desiredOrbit = -desiredOrbit;
            }

            return desiredOrbit.normalized;
        }
    }
}

public enum C_Mode
{
    NULL = -1,
    RoundBoundary = 0,
    StartAndEnd = 1,
    ScreenSaver = 3,
    OneVOne = 4,
    CertainYAntiVibration = 12,
    WatchOver = 8,
    TopDown = 2,
    GodPlayerCertainYCamera = 10,
    keepTargetLeft = 13,
    ApproachToCertainDis = 14,
}
