# IK Retargeter

Unity Montage includes a Humanoid-first IK Retargeter workflow. Unity's Avatar system performs the base animation retarget, while the Montage tools add semantic chain mapping, pose correction, IK goals, dual-character preview, and target-skeleton clip baking.

## Create a retargeter

1. Create **Assets > Create > Montage > IK Retargeter**.
2. Open **Window > Animation > Unity Montage > IK Retargeter**.
3. Assign the source character, target character, and source AnimationClip.
4. Click **Auto Configure Humanoid**. The tool assigns both Avatars and creates Root, Spine, arm, leg, and finger mappings.
5. Use the source and target Retarget Pose assets for character-specific local rotation or position corrections.

Both models must import as Humanoid for the live semantic-bone preview. The source and target are shown together: source chains are blue, target chains are orange. Use right mouse drag to orbit, middle mouse drag to pan, the wheel to zoom, and `F` to frame both characters.

## Runtime IK goals

Add `MontageIKRetargeter` beside the target character's Animator, assign the Retargeter asset, and configure hand or foot Goal bindings. Each binding accepts a target Transform, optional pole Transform, and separate position/rotation weights. The component runs after regular animation in `LateUpdate` and applies Retarget Pose corrections before its built-in two-bone solver.

The solver does not require the Animation Rigging package. Avoid non-uniform scale on limb parents because it makes analytic limb lengths ambiguous.

## Bake to the target skeleton

Click **Bake Retargeted Clip** to sample the source clip on the target character, apply root-translation scaling and Retarget Pose corrections, and write transform curves for the target hierarchy. The generated `.anim` is an ordinary AnimationClip and can be placed directly in a Montage segment.

Humanoid live preview and baking are the supported MVP route. Generic transform-path clips can be baked when source and target paths are compatible; UE-style arbitrary Generic chain resampling is not yet implemented.
