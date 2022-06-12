using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementController : MonoBehaviour
{
    #region Variables
    // --------------- Objects Variables ---------------
    private Rigidbody2D body;
    private Ground ground;
    private GameInputActions gameInputActions;

    // Vector3 to start the Raycast from
    private Vector3 cellingRayStartPosition;
    // Vector2 to save the input from the user
    private Vector2 inputVector2;
    //Vector2 to control and adjust player velocity
    private Vector2 totalVelocity;
    private Vector2 desiredVelocity;

    [Header("Movement Variables")]
    [SerializeField, Range(0f, 50f)] private float maxSpeed = 15f;
    [Space]
    [Header("Jump Variables")]
    [SerializeField, Range(0f, 50f)] private float jumpHeight = 30f;
    [SerializeField, Range(0f, 10f)] private float downwardMovementMultiplier = 8.5f;
    [SerializeField, Range(0f, 10f)] private float upwardMovementMultiplier = 8.5f;
    [SerializeField, Range(0, 5)] private int maxAirJumps = 2;
    [Space]
    [Header("Dash Variables")]
    [SerializeField, Range(0f, 100f)] private float dashingPower = 65f;
    [SerializeField, Range(0f, 1f)] private float dashingTime = 0.075f;
    [SerializeField, Range(0f, 10f)] private float dashingCooldown = 1f;
    [SerializeField] private bool canDashInAir = true;
    [Space]
    [Header("Crouch Variables")]
    [SerializeField] private float crouchingHeight = 0.675f;
    [SerializeField, Range(0f, 1f)] private float cellingRayDistance = 0.5f;
    [Space]
    [Header("Misc Variables")]
    // LayerMask save all the objects in the Ground layer
    [SerializeField] private LayerMask groundLayer;

    // check if the player is touching the ground
    private bool onGround;
    // check if the player is facing right
    private bool facingRight = true;

    // --------------- Jump Variables ---------------
    private int numOfCurrentJumps = 0;

    // --------------- Crouch Variables ---------------
    private bool canCrouch = true;
    private bool isCrouching = false;
    private bool canStand = false;
    private bool crouchToggle = false;

    // --------------- Dash Variables ---------------
    private bool canDash = true;
    private bool isDashing = false;

    #endregion

    // Get called when the script instance is being loaded
    void Awake()
    {
        // Grab referance for the GameObject components
        body = GetComponent<Rigidbody2D>();
        ground = GetComponent<Ground>();

        // Instantiates input actions functions
        gameInputActions = new GameInputActions();
    }

    // This function is called when the object becomes enabled and active.
    private void OnEnable()
    {
        // Enable the "Gameplay" action map
        gameInputActions.Gameplay.Enable();
    }

    // This function is called when the object becomes disabled or destroyed.
    private void OnDisable()
    {
        // Disable the "Gameplay" action map
        gameInputActions.Gameplay.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        // If the player is dashing skip this function
        if (isDashing) return;

        // Draw a ray for debug purposes 
        Debug.DrawRay(transform.position, Vector2.up * cellingRayDistance, Color.red);
        // Move the base of the ray with the player
        cellingRayStartPosition = transform.position;

        // If the player is crouching - check if the player can stand
        if (isCrouching) canStand = CanStand();

        // Get movement input from the user
        inputVector2 = gameInputActions.Gameplay.Movement.ReadValue<Vector2>();
        // Calculate desired velocity for the X-Axis
        desiredVelocity = new Vector2(inputVector2.x, 0f) * Mathf.Max(maxSpeed - ground.GetFriction(), 0f);

        // Check where the player moving to determine the player direction
        if (inputVector2.x < 0 && facingRight)
        {
            Flip(); // Flip the player to face left
        }
        else if (inputVector2.x > 0 && !facingRight)
        {
            Flip(); // Flip the player to face right
        }
    }

    // Update is called once per fixed-frame
    void FixedUpdate()
    {
        if (isDashing) return;

        onGround = ground.GetOnGround();
        totalVelocity = body.velocity;
        if (onGround)
        {
            numOfCurrentJumps = 0;
            if (!isCrouching)
            {
                canCrouch = true;
            }
        }

        totalVelocity.x = desiredVelocity.x;

        body.velocity = totalVelocity;
    }

    // Flip the player diraction - R2L or L2R
    private void Flip()
    {
        // Switch the way the player is labelled as facing.
        facingRight = !facingRight;

        // Multiply the player's x local scale by -1.
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    // Jump event
    public void JumpAction(InputAction.CallbackContext context)
    {
        // Ignore action input if dashing
        if (isDashing) return;
        // Ignore action input if phase != performed
        if (!context.performed) return;
        /*
         * If player is crouching and can stand stop crouching
         * ELSE 
         * If player is crouching and can't stand skip jump
        */
        if (isCrouching && canStand) CrouchAction(context);
        else if (isCrouching && !canStand) return;

        // Add a jump to current jumps counter
        numOfCurrentJumps += 1;
        // Check if player on the ground and allowed to jump
        /*
         * If player is on the ground AND allowed to jump (maxAirJumps != 0)
         * OR 
         * If player's jump counter haven't reached the max
         * THEN Jump
        */
        if ((onGround && maxAirJumps != 0) || numOfCurrentJumps < maxAirJumps)
        {
            // Calcultation for the jump force - Sqrt( -2 * -9.81 * jumpHeight)
            float jumpForce = Mathf.Sqrt(-2f * Physics2D.gravity.y * jumpHeight);

            /*
             * If player is moving up
             * ELSE
             * If player is moving down or stands still
            */
            if (totalVelocity.y > 0f)
            {
                // Apply upward multiplier tp gravity
                body.gravityScale = upwardMovementMultiplier;
                // Adjust new jump force
                jumpForce = Mathf.Max(jumpForce - totalVelocity.y, 0f);
            }
            else if (totalVelocity.y <= 0f)
            {
                // Apply downward multiplier tp gravity
                body.gravityScale = downwardMovementMultiplier;
                // Increase jump force
                jumpForce += Mathf.Abs(body.velocity.y);
            }

            // Apply jump force to player
            totalVelocity.y += jumpForce;
            body.velocity = totalVelocity;
        }
    }

    // Crouch event
    public void CrouchAction(InputAction.CallbackContext context)
    {
        // Ignore action input if dashing
        if (isDashing) return;
        // Ignore action input if phase != performed
        if (!context.performed) return;

        /* 
         * If player is on the ground AND can crouch AND isn't crouching AND toggle is off - Crouch
         * ELSE
         * If player is crouching AND can stand AND toggle is on - Stand
        */
        if (onGround && canCrouch && !isCrouching && !crouchToggle)
        {
            // Active toggle function - turn on toggle
            ToggleCrouch();
            // Set player status to crouching
            isCrouching = true;
            // Set player new dimensions
            transform.localScale = new(transform.localScale.x, transform.localScale.y * crouchingHeight);
        }
        else if (isCrouching && canStand && crouchToggle)
        {
            // Active toggle function - turn off toggle
            ToggleCrouch();
            // Set player status to standing
            isCrouching = false;
            // Restore player old dimensions
            transform.localScale = new(transform.localScale.x, transform.localScale.y / crouchingHeight);
        }
    }

    // Dash Event
    public void DashAction(InputAction.CallbackContext context)
    {
        // Ignore action input if phase != performed
        if (!context.performed) return;

        /*
         * If player is on the ground AND can dash
         * OR
         * If player can dash AND can dash in the air - OPTIONAL
         * THEN dash
        */
        if ((onGround && canDash) || (canDash && canDashInAir))
        {
            // Start the dash coroutine
            StartCoroutine(Dash());
        }
    }

    // Dash Coroutine - Starts when the dash event happens
    private IEnumerator Dash()
    {
        // Set player status to dashing
        isDashing = true;
        // Turn off dashing abillity
        canDash = false;
        // Set player gravity to 0
        body.gravityScale = 0f;
        // Move player - direction (L or R) * dashing power for X-Axis and 0 for Y-Axis
        body.velocity = new Vector2(transform.localScale.x * dashingPower, 0f);
        // Wait to stop the dash
        yield return new WaitForSeconds(dashingTime);
        // Set player status to not dashing
        isDashing = false;
        // Restore player gravity
        body.gravityScale = downwardMovementMultiplier;
        // Wait for a cooldown to dash again
        yield return new WaitForSeconds(dashingCooldown);
        canDash = true;
    }

    // Check if the player have enough space to stand when crouched
    private bool CanStand()
    {
        // Set direction for the ray
        Vector2 direction = Vector2.up;
        // Cast a ray from the start point, upward, how far, and what target to hit
        RaycastHit2D hit = Physics2D.Raycast(cellingRayStartPosition, direction, cellingRayDistance, groundLayer);
        // Check if the ray had hit 
        if (hit.collider != null)
        {
            // the ray hit a ground/celling and player CAN'T stand
            return false;
        }
        // the ray didn't hit a ground/celling and player CAN stand
        return true;
    }

    // Toggle crouch function so the player don't have to hold the "Crouch" button
    private void ToggleCrouch()
    {
        crouchToggle = !crouchToggle;
    }
}