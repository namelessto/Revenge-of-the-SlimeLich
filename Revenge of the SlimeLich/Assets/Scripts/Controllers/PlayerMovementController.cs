using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovementController : MonoBehaviour
{
    #region Serializable Variables

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
    [Header("Climb Variables")]
    [SerializeField, Range(0f, 50f)] private float climbSpeed = 15f;
    [SerializeField, Range(0f, 50f)] private float thrust = 3f;
    [Space]
    [Header("Misc Variables")]
    // LayerMask save all the objects in the Ground layer
    [SerializeField] private LayerMask groundLayer;

    #endregion

    #region Private Variables

    // --------------- Objects Variables ---------------
    private Rigidbody2D body;
    private Ground ground;
    private GameInputActions gameInputActions;

    // Vector3 to start the Raycast from
    private Vector3 cellingRayStartPosition;
    // Vector2 to save the input from the user
    private Vector2 inputVector2;
    //Vector2 to control and adjust player velocity
    private Vector2 desiredVelocity;

    // check if the player is touching the ground
    private bool onGround;
    // check if the player is facing right
    private bool facingRight = true;

    // --------------- Movement Variables ---------------
    private float currentSpeed = 15f;

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

    // --------------- Climb Variables ---------------
    private bool canClimb = false;
    private bool isClimbing = false;

    #endregion

    #region Unity Built-In Methods

    // Get called when the script instance is being loaded
    void Awake()
    {
        // Grab referance for the GameObject components
        body = GetComponent<Rigidbody2D>();
        ground = GetComponent<Ground>();

        // Instantiates input actions functions
        gameInputActions = new GameInputActions();

        body.gravityScale = downwardMovementMultiplier;
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

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            canClimb = true;
            if (isClimbing)
            {
                body.gravityScale = 0f;
                desiredVelocity.y = Climb(GetInput(), tryToClimb: true);
                body.velocity = desiredVelocity;
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Ladder"))
        {
            if (isClimbing)
            {
                body.AddForce(GetInput() * thrust, ForceMode2D.Impulse);
            }
            body.gravityScale = downwardMovementMultiplier;
            canClimb = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // If the player is dashing skip this function
        if (isDashing) return;

        onGround = ground.GetOnGround();
        
        // Draw a ray for debug purposes 
        Debug.DrawRay(transform.position, Vector2.up * cellingRayDistance, Color.red);
        // Move the base of the ray with the player
        cellingRayStartPosition = transform.position;

        // If the player is crouching - check if the player can stand
        if (isCrouching) canStand = CanStand();

        // Get movement input from the user
        inputVector2 = GetInput();

        if (onGround)
        {
            numOfCurrentJumps = 0;
            if (isClimbing)
            {
                currentSpeed = inputVector2.x * Mathf.Max(maxSpeed / 2f, 0f);
            }
            else if (isCrouching)
            {
                currentSpeed = inputVector2.x * Mathf.Max((maxSpeed - ground.GetFriction()) * 0.75f, 0f);
            }
            else if (!canClimb || !isCrouching)
            {
                isClimbing = false;
                canCrouch = true;
                currentSpeed = inputVector2.x * Mathf.Max(maxSpeed - ground.GetFriction(), 0f);
            }
        }
        else
        {
            currentSpeed = inputVector2.x * Mathf.Max(maxSpeed, 0f);
        }

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

        desiredVelocity = body.velocity;

        desiredVelocity.x = currentSpeed;

        body.velocity = desiredVelocity;
    }

    #endregion

    #region Custom Methods

    private Vector2 GetInput()
    {
        return gameInputActions.Gameplay.Movement.ReadValue<Vector2>();
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
        // Restore player gravity
        body.gravityScale = downwardMovementMultiplier;
        // Set player status to not dashing
        isDashing = false;
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

    private float Climb(Vector2 input, bool tryToClimb)
    {
        if (!tryToClimb)
        {
            isClimbing = false;
        }
        else
        {
            desiredVelocity.y = input.y * climbSpeed;
            isClimbing = true;
        }
        return desiredVelocity.y;
    }

    #endregion

    #region Input System Actions

    // Movement / Climb Event
    public void MovementAction(InputAction.CallbackContext context)
    {
        // Get player input
        Vector2 input = context.ReadValue<Vector2>();

        /*
         * If player can climb and press UP/DOWN active climbing
         * ELSE
         * Disable climbing
        */
        if (canClimb && Mathf.Abs(input.y) > 0f)
        {
            // Set player gravity to zero
            body.gravityScale = 0f;
            // Get climbing force
            desiredVelocity.y = Climb(input, tryToClimb: true);
            // Apply 
            body.velocity = desiredVelocity;
        }
        else
        {
            body.gravityScale = downwardMovementMultiplier;
            desiredVelocity.y = Climb(input, tryToClimb: false);
            body.velocity = desiredVelocity;
        }
    }

    // Jump event
    public void JumpAction(InputAction.CallbackContext context)
    {
        // Ignore action input if dashing
        if (isDashing) return;
        // Ignore action input if phase != performed
        if (context.started) return;

        /*
         * If player is crouching and can stand stop crouching
         * ELSE 
         * If player is crouching and can't stand skip jump
        */
        if (isCrouching && canStand) CrouchAction(context);
        else if (isCrouching && !canStand) return;

        /*
         * If player is on the ground AND allowed to jump (maxAirJumps != 0)
         * OR 
         * If player's jump counter haven't reached the max
         * THEN Jump
        */
        if (context.performed)
        {
            // Add a jump to current jumps counter
            numOfCurrentJumps += 1;

            if ((onGround && maxAirJumps != 0) || numOfCurrentJumps <= maxAirJumps)
            {
                // Calcultation for the jump force - Sqrt( -2 * -9.81 * jumpHeight)
                float jumpForce = Mathf.Sqrt(-2f * Physics2D.gravity.y * jumpHeight);

                /*
                 * If player is moving up
                 * ELSE
                 * If player is moving down or stands still
                */
                if (desiredVelocity.y > 0f || onGround)
                {
                    // Apply upward multiplier tp gravity
                    body.gravityScale = upwardMovementMultiplier;
                    // Adjust new jump force
                    jumpForce = Mathf.Max(jumpForce - desiredVelocity.y, 0f);
                }
                else if (desiredVelocity.y <= 0f)
                {
                    // Apply downward multiplier tp gravity
                    body.gravityScale = downwardMovementMultiplier;
                    // Increase jump force
                    jumpForce += Mathf.Abs(body.velocity.y);
                }

                // Apply jump force to player
                desiredVelocity.y += jumpForce;
                body.velocity = desiredVelocity;
            }
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

    #endregion
}