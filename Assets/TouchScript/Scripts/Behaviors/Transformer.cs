/*
 * @author Valentin Simonov / http://va.lent.in/
 */

using System;
using System.Collections.Generic;
using TouchScript.Gestures;
using TouchScript.Gestures.TransformGestures;
using TouchScript.Gestures.TransformGestures.Base;
using TouchScript.Utils.Attributes;
using UnityEngine;

namespace TouchScript.Behaviors
{
    /// <summary>
    /// Component which transforms an object according to events from transform gestures: <see cref="TransformGesture"/>, <see cref="ScreenTransformGesture"/>, <see cref="PinnedTransformGesture"/> and others.
    /// </summary>
    [AddComponentMenu("TouchScript/Behaviors/Transformer")]
    [HelpURL("http://touchscript.github.io/docs/html/T_TouchScript_Behaviors_Transformer.htm")]
    public class Transformer : MonoBehaviour
    {
        // Here's how it works.
        //
        // If smoothing is not enabled, the component just gets gesture events in stateChangedHandler(), passes Changed event to manualUpdate() which calls applyValues() to sett updated values.
        // The value of transformMask is used to only set values which were changed not to interfere with scripts changing this values.
        //
        // If smoothing is enabled — targetPosition, targetScale, targetRotation are cached and a lerp from current position to these target positions is applied every frame in update() method. It also checks transformMask to change only needed values.
        // If none of the delta values pass the threshold, the component transitions to idle state.

        #region Consts

        /// <summary>
        /// State for internal Transformer state machine.
        /// </summary>
        private enum TransformerState
        {
            /// <summary>
            /// Nothing is happening.
            /// </summary>
            Idle,

            /// <summary>
            /// The object is under manual control, i.e. user is transforming it.
            /// </summary>
            Manual,

            /// <summary>
            /// The object is under automatic control, i.e. it's being smoothly moved into target position when user lifted all fingers off.
            /// </summary>
            Automatic
        }

        #endregion

        #region Public properties


        /// <summary>
        /// Gets or sets a value indicating whether Smoothing is enabled. Smoothing allows to reduce jagged movements but adds some visual lag.
        /// </summary>
        /// <value>
        ///   <c>true</c> if Smoothing is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool EnableSmoothing
        {
            get { return enableSmoothing; }
            set { enableSmoothing = value; }
        }

        /// <summary>
        /// Gets or sets the smoothing factor.
        /// </summary>
        /// <value>
        /// The smoothing factor. Indicates how much smoothing to apply. 0 - no smoothing, 100000 - maximum.
        /// </value>
        public float SmoothingFactor
        {
            get { return smoothingFactor * 100000f; }
            set { smoothingFactor = Mathf.Clamp(value / 100000f, 0, 1); }
        }

        /// <summary>
        /// Gets or sets the position threshold.
        /// </summary>
        /// <value>
        /// Minimum distance between target position and smoothed position when to stop automatic movement.
        /// </value>
        public float PositionThreshold
        {
            get { return Mathf.Sqrt(positionThreshold); }
            set { positionThreshold = value * value; }
        }

        /// <summary>
        /// Gets or sets the rotation threshold.
        /// </summary>
        /// <value>
        /// Minimum angle between target rotation and smoothed rotation when to stop automatic movement.
        /// </value>
        public float RotationThreshold
        {
            get { return rotationThreshold; }
            set { rotationThreshold = value; }
        }

        /// <summary>
        /// Gets or sets the scale threshold.
        /// </summary>
        /// <value>
        /// Minimum difference between target scale and smoothed scale when to stop automatic movement.
        /// </value>
        public float ScaleThreshold
        {
            get { return Mathf.Sqrt(scaleThreshold); }
            set { scaleThreshold = value * value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this transform can be changed from another script.
        /// </summary>
        /// <value>
        /// <c>true</c> if this transform can be changed from another script; otherwise, <c>false</c>.
        /// </value>
        public bool AllowChangingFromOutside
        {
            get { return allowChangingFromOutside; }
            set { allowChangingFromOutside = value; }
        }

        #endregion

        #region Private variables

        [SerializeField]
        [ToggleLeft]
        private bool enableSmoothing = false;

        [SerializeField]
        private float smoothingFactor = 1f / 100000f;

        [SerializeField]
        private float positionThreshold = 0.01f;

        [SerializeField]
        private float rotationThreshold = 0.1f;

        [SerializeField]
        private float scaleThreshold = 0.01f;

        [SerializeField]
        [ToggleLeft]
        private bool allowChangingFromOutside = false;

        private TransformerState state;

        private TransformGestureBase gesture;
        private Transform cachedTransform;

        private TransformGesture.TransformType transformMask;
        private Vector3 targetPosition, targetScale;
        private Quaternion targetRotation;

        // last* variables are needed to detect when Transform's properties were changed outside of this script
        private Vector3 lastPosition, lastScale;
        private Quaternion lastRotation;

        #endregion

        #region Unity methods
        GameObject work_row, drop_cells, spawn_cells;
        const int BEE_FREE = 1;
        const int BEE_RESTRICTED = 0;

        const int FROM_LEFT = 0;
        const int FROM_RIGHT = 1;

        private int from_where = 0;



        private int player_type; //0 = restricted, 1 = free
        private float SPRITE_WIDTH;
        private void Start()
        {

            work_row = GameObject.FindGameObjectWithTag("work_row_middle");
            drop_cells = GameObject.FindGameObjectWithTag("drop_cells");
            spawn_cells = GameObject.FindGameObjectWithTag("spawn_cells");

            if (gameObject.tag.Equals("bee_free")) { player_type = BEE_FREE; }
            if (gameObject.tag.Equals("bee_restricted")) { player_type = BEE_RESTRICTED; }

            if (transform.position.x < 0)
            {
                from_where = FROM_LEFT;
            }
            else
            {
                from_where = FROM_RIGHT;
            }
            SPRITE_WIDTH = GetComponent<SpriteRenderer>().sprite.bounds.size.x;

            //    Debug.Log(" sprite width = " + GetComponent<SpriteRenderer>().sprite.bounds.size.x);
        }

        private void Awake()
        {
            cachedTransform = transform;
        }

        private void OnEnable()
        {
            gesture = GetComponent<TransformGestureBase>();
            gesture.StateChanged += stateChangedHandler;
            TouchManager.Instance.FrameFinished += frameFinishedHandler;

            stateIdle();
        }

        private void OnDisable()
        {
            if (gesture != null) gesture.StateChanged -= stateChangedHandler;
            if (TouchManager.Instance != null)
                TouchManager.Instance.FrameFinished -= frameFinishedHandler;

            stateIdle();
        }

        #endregion

        #region States

        private void stateIdle()
        {
            var prevState = state;
            setState(TransformerState.Idle);

            if (enableSmoothing && prevState == TransformerState.Automatic)
            {
                transform.position = lastPosition = targetPosition;
                var newLocalScale = lastScale = targetScale;
                // prevent recalculating colliders when no scale occurs
                if (newLocalScale != transform.localScale) transform.localScale = newLocalScale;
                transform.rotation = lastRotation = targetRotation;
            }

            transformMask = TransformGesture.TransformType.None;
        }

        private void stateManual()
        {
            setState(TransformerState.Manual);

            targetPosition = lastPosition = cachedTransform.position;
            targetRotation = lastRotation = cachedTransform.rotation;
            targetScale = lastScale = cachedTransform.localScale;
            transformMask = TransformGesture.TransformType.None;
        }

        private void stateAutomatic()
        {
            setState(TransformerState.Automatic);

            if (!enableSmoothing || transformMask == TransformGesture.TransformType.None) stateIdle();
        }

        private void setState(TransformerState newState)
        {
            state = newState;
        }

        #endregion

        #region Private functions

        private void update()
        {
            if (state == TransformerState.Idle) return;

            if (!enableSmoothing) return;

            var fraction = 1 - Mathf.Pow(smoothingFactor, Time.unscaledDeltaTime);
            var changed = false;

            if ((transformMask & TransformGesture.TransformType.Scaling) != 0)
            {
                var scale = transform.localScale;
                if (allowChangingFromOutside)
                {
                    // Changed by someone else.
                    // Need to make sure to check per component here.
                    if (!Mathf.Approximately(scale.x, lastScale.x))
                        targetScale.x = scale.x;
                    if (!Mathf.Approximately(scale.y, lastScale.y))
                        targetScale.y = scale.y;
                    if (!Mathf.Approximately(scale.z, lastScale.z))
                        targetScale.z = scale.z;
                }
                var newLocalScale = Vector3.Lerp(scale, targetScale, fraction);
                // Prevent recalculating colliders when no scale occurs.
                if (newLocalScale != scale)
                {
                    transform.localScale = newLocalScale;
                    // Something might have adjusted our scale.
                    lastScale = transform.localScale;
                }

                if (state == TransformerState.Automatic && !changed && (targetScale - lastScale).sqrMagnitude > scaleThreshold) changed = true;
            }

            if ((transformMask & TransformGesture.TransformType.Rotation) != 0)
            {
                if (allowChangingFromOutside)
                {
                    // Changed by someone else.
                    if (transform.rotation != lastRotation) targetRotation = transform.rotation;
                }
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, fraction);
                // Something might have adjusted our rotation.
                lastRotation = transform.rotation;

                if (state == TransformerState.Automatic && !changed && Quaternion.Angle(targetRotation, lastRotation) > rotationThreshold) changed = true;
            }

            if ((transformMask & TransformGesture.TransformType.Translation) != 0)
            {
                var pos = transform.position;
                if (allowChangingFromOutside)
                {
                    // Changed by someone else.
                    // Need to make sure to check per component here.
                    if (!Mathf.Approximately(pos.x, lastPosition.x))
                        targetPosition.x = pos.x;
                    if (!Mathf.Approximately(pos.y, lastPosition.y))
                        targetPosition.y = pos.y;
                    if (!Mathf.Approximately(pos.z, lastPosition.z))
                        targetPosition.z = pos.z;
                }
                transform.position = Vector3.Lerp(pos, targetPosition, fraction);
                // Something might have adjusted our position (most likely Unity UI).
                lastPosition = transform.position;

                if (state == TransformerState.Automatic && !changed && (targetPosition - lastPosition).sqrMagnitude > positionThreshold) changed = true;
            }

            if (state == TransformerState.Automatic && !changed) stateIdle();
        }

        private void manualUpdate()
        {
            if (state != TransformerState.Manual) stateManual();

            var mask = gesture.TransformMask;
            if ((mask & TransformGesture.TransformType.Scaling) != 0) targetScale *= gesture.DeltaScale;
            if ((mask & TransformGesture.TransformType.Rotation) != 0)
                targetRotation = Quaternion.AngleAxis(gesture.DeltaRotation, gesture.RotationAxis) * targetRotation;
            if ((mask & TransformGesture.TransformType.Translation) != 0) targetPosition += gesture.DeltaPosition;
            transformMask |= mask;

            gesture.OverrideTargetPosition(targetPosition);

            if (!enableSmoothing) applyValues();
        }

        private void applyValues()
        {
            if ((transformMask & TransformGesture.TransformType.Scaling) != 0) cachedTransform.localScale = targetScale;
            if ((transformMask & TransformGesture.TransformType.Rotation) != 0) cachedTransform.rotation = targetRotation;

            if ((transformMask & TransformGesture.TransformType.Translation) != 0)
            {

                checkBoundaries();
            }

            transformMask = TransformGesture.TransformType.None;
        }

        #endregion

        #region Event handlers

        private void stateChangedHandler(object sender, GestureStateChangeEventArgs gestureStateChangeEventArgs)
        {
            switch (gestureStateChangeEventArgs.State)
            {
                case Gesture.GestureState.Possible:
                    stateManual();
                    break;
                case Gesture.GestureState.Changed:
                    manualUpdate();
                    break;
                case Gesture.GestureState.Ended:
                case Gesture.GestureState.Cancelled:
                    stateAutomatic();
                    break;
                case Gesture.GestureState.Failed:
                case Gesture.GestureState.Idle:
                    if (gestureStateChangeEventArgs.PreviousState == Gesture.GestureState.Possible) stateAutomatic();
                    break;
            }
        }

        private void frameFinishedHandler(object sender, EventArgs eventArgs)
        {
            update();
        }

        private void checkBoundaries()
        {
            Vector3 targetPosition2 = targetPosition;
            //108 is the width of square, 104 the width of disk.
            // Let the player enter the work cell a little bit so they can be in touch with the pellet
            // in order to grab it. Hence the 0.8f coefficient.
            float barrier_right = work_row.transform.GetChild(0).position.x + 0.8f * (SPRITE_WIDTH / 2);
            float barrier_left = work_row.transform.GetChild(0).position.x - 0.8f * (SPRITE_WIDTH / 2);
            float barrier_top = work_row.transform.GetChild(work_row.transform.childCount - 1).position.y + 1.5f * (SPRITE_WIDTH / 2);
            float barrier_drop = drop_cells.transform.GetChild(0).position.x + 0.9f * (SPRITE_WIDTH);
            float barrier_spawn = spawn_cells.transform.GetChild(0).position.x - 0.8f * (SPRITE_WIDTH / 2);

            if (targetPosition.x < barrier_drop + SPRITE_WIDTH / 2) targetPosition2.x = barrier_drop + SPRITE_WIDTH / 2;
            if (targetPosition.x > barrier_spawn - SPRITE_WIDTH / 2) targetPosition2.x = barrier_spawn - SPRITE_WIDTH / 2;

            //coming from the top right
            if (from_where == FROM_RIGHT && targetPosition.x < barrier_right + SPRITE_WIDTH / 2 && targetPosition.y < barrier_top)
            {
                targetPosition2.x = barrier_right + SPRITE_WIDTH / 2;
            }
            else
            if (from_where == FROM_LEFT && targetPosition.x > barrier_left - SPRITE_WIDTH / 2 && targetPosition.y < barrier_top)
            {
                targetPosition2.x = barrier_left - SPRITE_WIDTH / 2;
            }

            cachedTransform.position = targetPosition2;

            if (targetPosition2.x < 0) { from_where = FROM_LEFT; }
            else { from_where = FROM_RIGHT; }

        }

        #endregion
    }
}