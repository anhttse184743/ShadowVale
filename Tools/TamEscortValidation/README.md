# Isolated Tâm escort validation

This local Unity project imports the original Nam/Tâm FBX models and the four authored escort FBX clips. TamEscortBuilder creates the same runtime clips copied into the main project. EscortRuntimeChecks.cs exercises compiled ShadowVale gameplay assemblies and renders the imported pose.

Generated model copies, packages, assemblies, Library and logs are excluded from Git. Durable results live in ../TamEscortReports. The runtime validation source remains in Assets/Editor/EscortRuntimeChecks.cs.

The validation project uses Unity 6000.3.24f1. Entrypoints: ShadowVale.Map01.Editor.TamEscortBuilder.Build, EscortRuntimeChecks.Run, and EscortRuntimeChecks.RenderPose. Use -batchmode -nographics -quit for checks, and omit -nographics for renders.
