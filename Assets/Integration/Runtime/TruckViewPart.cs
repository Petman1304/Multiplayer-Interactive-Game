using UnityEngine;

namespace Getaway.Integration
{
    /// <summary>Local camera visibility for a render-only truck mesh; never changes source assets.</summary>
    public sealed class TruckViewPart : MonoBehaviour
    {
        public bool Cockpit;
        public bool InteriorDuplicate;
    }
}
