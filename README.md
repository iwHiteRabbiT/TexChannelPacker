# Tex Channel Packer
*From Megascans to Unreal*

Texture Channel Packer destined to help the workflow between Quixel Megascans & Unreal Engine.

Create 2 textures:
```
MatName_AAOS.png = (RGB)Albedo * AO ; (A)Spec^2 * AO^2
```
```
MatName_NDR.png = (RG)Normal_X, 1 - Normal_Y ; (B)Displacement ; (A)Roughness 
```
