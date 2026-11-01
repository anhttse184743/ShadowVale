using System.Runtime.CompilerServices;

// Map 2 (its own assembly since the Map 2 split) still uses a few of Map 1's internal helpers, e.g. Map01SkinContact.
[assembly: InternalsVisibleTo("ShadowVale.Map02")]
