Level2 notes:
- Air belts now use AirBeltTrigger component which applies acceleration when a Rigidbody stays in trigger.
- Arrow visuals (air arrow N) are child objects of belts. Materials change color to indicate direction.
- If belt triggers don't affect the ball, check that the ball's Collider is not set as trigger and has Rigidbody attached.
- Rebuild Level02 scene in Editor to pick up new objects: Hand Of God → Rebuild Level 02 Scene.