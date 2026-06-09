using UnityEngine;

namespace ThronefallMP.Components;

// Client-side smoothing of host position corrections for locally-predicted entities (enemies/allies).
// The entity keeps moving each frame under its own local pathfinding prediction; this component folds the
// authoritative host correction in as an exponentially-decaying offset, so the entity tracks the host tightly
// without trailing (the old soft distance-weighted Lerp) or snapping/rubber-banding (a hard position write).
public class PositionErrorSmoother : MonoBehaviour
{
    // Catch-up rate. Higher = faster. k = 12 absorbs ~90% of the remaining error in ~190ms.
    public float K = 12f;

    private Vector3 _pendingError;

    // Called on each received authoritative position. Sets the gap to absorb, or hard-snaps on large jumps
    // (teleport / respawn / late spawn / big divergence) so we don't slowly crawl across the map.
    public void ApplyCorrection(Vector3 hostPosition, float snapDistance)
    {
        var error = hostPosition - transform.position;
        if (error.sqrMagnitude > snapDistance * snapDistance)
        {
            transform.position = hostPosition;
            _pendingError = Vector3.zero;
        }
        else
        {
            _pendingError = error;
        }
    }

    private void LateUpdate()
    {
        if (_pendingError == Vector3.zero)
        {
            return;
        }

        var t = 1f - Mathf.Exp(-K * Time.deltaTime);
        var applied = _pendingError * t;
        transform.position += applied;
        _pendingError -= applied;
        if (_pendingError.sqrMagnitude < 1e-6f)
        {
            _pendingError = Vector3.zero;
        }
    }
}
