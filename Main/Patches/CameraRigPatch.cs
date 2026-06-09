using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ThronefallMP.Patches;

public static class CameraRigPatch
{
    public static void Apply()
    {
        On.CameraRig.Update += Update;
        On.CameraRig.TransitionToTarget += TransitionToTarget;
    }

    private static void Update(On.CameraRig.orig_Update original, CameraRig self)
    {
        var cameraTarget = Traverse.Create(self).Field<Transform>("cameraTarget");
        var localData = Plugin.Instance.PlayerManager.LocalPlayer?.Data;
        if (localData != null)
        {
            cameraTarget.Value = localData.transform;
            original(self);
        }
    }

    private static IEnumerator TransitionToTarget(On.CameraRig.orig_TransitionToTarget original, CameraRig self, Transform newTarget, float targetCameraSize)
    {
        // NOTE: game 2024+ added 'targetCameraSize' and removed the private 'targetPosition' field that this
        // reimplementation used as its loop-exit condition. We compare against the camera's actual position
        // instead, which terminates the transition exactly when the camera reaches the target, and we lerp the
        // orthographic size toward targetCameraSize in lockstep with the pan (matching vanilla TransitionToTarget).
        var transitionRunning = Traverse.Create(self).Field<bool>("transitionRunning");
        var transitionSpeed = Traverse.Create(self).Field<float>("transitionSpeed");
        var currentTarget = Traverse.Create(self).Field<Transform>("currentTarget");
        var cameras = Traverse.Create(self).Field<List<Camera>>("cameras").Value;

        transitionRunning.Value = true;
        var startPosition = self.transform.position;
        var startRotation = self.transform.rotation;
        var startCameraSize = cameras != null && cameras.Count > 0 ? cameras[0].orthographicSize : targetCameraSize;
        var transitionTime = 0f;
        while (newTarget != null && (self.transform.position != newTarget.position || self.transform.rotation != newTarget.rotation))
        {
            transitionTime = Mathf.Clamp(transitionTime, 0f, 1f);
            var num = 3f * Mathf.Pow(transitionTime, 2f) - 2f * Mathf.Pow(transitionTime, 3f);
            self.transform.position = Vector3.Lerp(startPosition, newTarget.position, num);
            self.transform.rotation = Quaternion.Lerp(startRotation, newTarget.rotation, num);
            if (cameras != null)
            {
                var size = Mathf.Lerp(startCameraSize, targetCameraSize, num);
                foreach (var cam in cameras) cam.orthographicSize = size;
            }
            transitionTime += Time.deltaTime * transitionSpeed.Value;
            yield return null;
        }

        if (cameras != null)
        {
            foreach (var cam in cameras) cam.orthographicSize = targetCameraSize;
        }
        currentTarget.Value = newTarget;
        transitionRunning.Value = false;
    }
}