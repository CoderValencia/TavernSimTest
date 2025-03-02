﻿// Copyright (c) 2015 - 2022 Doozy Entertainment. All Rights Reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement
// A Copy of the EULA APPENDIX 1 is available at http://unity3d.com/company/legal/as_terms

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Doozy.Runtime.Common.Extensions;
using Doozy.Runtime.Global;
using Doozy.Runtime.Mody;
using Doozy.Runtime.Reactor;
using Doozy.Runtime.Reactor.Internal;
using Doozy.Runtime.Reactor.Reactions;
using Doozy.Runtime.UIManager.Events;
using Doozy.Runtime.UIManager.Input;
using Doozy.Runtime.UIManager.ScriptableObjects;
using Doozy.Runtime.UIManager.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Local

namespace Doozy.Runtime.UIManager.Containers
{
    /// <summary>
    /// Basic container with show and hide capabilities.
    /// All other containers use this as their base.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Doozy/UI/Containers/UI Container")]
    [SelectionBase]
    public class UIContainer : MonoBehaviour, ICanvasElement, IUseMultiplayerInfo
    {
        /// <summary> Stream category name </summary>
        public const string k_StreamCategory = nameof(UIContainer);
        /// <summary> Default animation duration </summary>
        public const float k_DefaultAnimationDuration = 0.3f;

        #region MultiplayerInfo

        [SerializeField] private MultiplayerInfo MultiplayerInfo;
        /// <summary> Reference to the MultiPlayerInfo component </summary>
        public MultiplayerInfo multiplayerInfo => MultiplayerInfo;

        /// <summary> Check if a MultiplayerInfo has been referenced </summary>
        public bool hasMultiplayerInfo => multiplayerInfo != null;

        /// <summary> Player index for this component </summary>
        public int playerIndex => multiplayerMode & hasMultiplayerInfo ? multiplayerInfo.playerIndex : inputSettings.defaultPlayerIndex;

        /// <summary> Set the a reference to a MultiplayerInfo </summary>
        /// <param name="reference"> MultiplayerInfo reference </param>
        public void SetMultiplayerInfo(MultiplayerInfo reference) =>
            MultiplayerInfo = reference;

        #endregion

        /// <summary> Reference to the UIManager Input Settings </summary>
        public static UIManagerInputSettings inputSettings => UIManagerInputSettings.instance;

        /// <summary> Check if Multiplayer Mode is enabled </summary>
        public static bool multiplayerMode => inputSettings.multiplayerMode;

        private Canvas m_Canvas;
        /// <summary> Reference to the Canvas component attached to this GameObject </summary>
        public Canvas canvas => m_Canvas ? m_Canvas : m_Canvas = GetComponent<Canvas>();

        private CanvasGroup m_CanvasGroup;
        /// <summary> Reference to the CanvasGroup component attached to this GameObject  </summary>
        public CanvasGroup canvasGroup => m_CanvasGroup ? m_CanvasGroup : m_CanvasGroup = GetComponent<CanvasGroup>();

        /// <summary> Check if a CanvasGroup component is attached to this GameObject  </summary>
        public bool hasCanvasGroup { get; private set; }

        private GraphicRaycaster m_GraphicRaycaster;
        /// <summary> Reference to the GraphicRaycaster component attached to this GameObject  </summary>
        public GraphicRaycaster graphicRaycaster => m_GraphicRaycaster ? m_GraphicRaycaster : m_GraphicRaycaster = GetComponent<GraphicRaycaster>();

        private RectTransform m_RectTransform;
        /// <summary> Reference to the RectTransform component attached to this GameObject  </summary>
        public RectTransform rectTransform => m_RectTransform ? m_RectTransform : m_RectTransform = GetComponent<RectTransform>();

        /// <summary> Behaviour on Start </summary>
        public ContainerBehaviour OnStartBehaviour = ContainerBehaviour.Disabled;

        #region Visibility

        private int m_LastFrameVisibilityStateChanged;

        /// <summary> Current visibility state </summary>
        private VisibilityState m_VisibilityState = VisibilityState.Visible;

        /// <summary> Current visibility state </summary>
        public VisibilityState visibilityState
        {
            get => m_VisibilityState;
            private set
            {
                m_LastFrameVisibilityStateChanged = Time.frameCount;
                m_VisibilityState = value;
                OnVisibilityChangedCallback?.Invoke(m_VisibilityState);
                switch (value)
                {
                    case VisibilityState.Visible:
                        ExecuteOnVisible();
                        break;
                    case VisibilityState.Hidden:
                        ExecuteOnHidden();
                        break;
                    case VisibilityState.IsShowing:
                        ExecuteOnShow();
                        break;
                    case VisibilityState.IsHiding:
                        ExecuteOnHide();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }

        /// <summary> Visibility state is Visible </summary>
        public bool isVisible => visibilityState == VisibilityState.Visible;

        /// <summary> Visibility state is Hidden </summary>
        public bool isHidden => visibilityState == VisibilityState.Hidden;

        /// <summary> Visibility state is IsShowing - Show animation is running </summary>
        public bool isShowing => visibilityState == VisibilityState.IsShowing;

        /// <summary> Visibility state is IsHiding - Hide animation is running </summary>
        public bool isHiding => visibilityState == VisibilityState.IsHiding;

        /// <summary> Visibility state is either IsShowing or IsHiding - either Show or Hide animation is running </summary>
        public bool inTransition => isShowing || isHiding;

        /// <summary> Show animation started - executed when visibility state changed to IsShowing </summary>
        public ModyEvent OnShowCallback;

        /// <summary> Visible - Show animation finished - executed when visibility state changed to Visible </summary>
        public ModyEvent OnVisibleCallback;

        /// <summary> Hide animation started - executed when visibility state changed to IsHiding </summary>
        public ModyEvent OnHideCallback;

        /// <summary> Hidden - Hide animation finished - callback invoked when visibility state changed to Hidden </summary>
        public ModyEvent OnHiddenCallback;

        /// <summary> Visibility changed - callback invoked when visibility state changed </summary>
        public VisibilityStateEvent OnVisibilityChangedCallback;

        [SerializeField] private List<Progressor> ShowProgressors;
        /// <summary> Progressors triggered on Show. Plays forward. </summary>
        public List<Progressor> showProgressors => ShowProgressors ?? (ShowProgressors = new List<Progressor>());

        [SerializeField] private List<Progressor> HideProgressors;
        /// <summary> Progressors triggered on Hide. Plays forward. </summary>
        public List<Progressor> hideProgressors => HideProgressors ?? (HideProgressors = new List<Progressor>());

        [SerializeField] private List<Progressor> ShowHideProgressors;
        /// <summary> Progressors triggered on both Show and Hide. Plays forward on Show and in reverse on Hide. </summary>
        public List<Progressor> showHideProgressors => ShowHideProgressors ?? (ShowHideProgressors = new List<Progressor>());

        /// <summary> Action invoked every time before the container needs to change its state </summary>
        public UnityAction<ShowHideExecute> showHideExecute { get; set; }

        /// <summary> Flag to keep track if the first show/hide command has been issued </summary>
        public bool executedFirstCommand { get; protected set; }

        /// <summary> Keeps track of the previously executed show/hide command </summary>
        public ShowHideExecute previouslyExecutedCommand { get; protected set; }

        #endregion

        /// <summary> AnchoredPosition3D to snap to on Awake </summary>
        public Vector3 CustomStartPosition;

        /// <summary> If TRUE, the rectTransform.anchoredPosition3D will 'snap' to the CustomStartPosition on Awake </summary>
        public bool UseCustomStartPosition;

        /// <summary> If TRUE, after this container gets shown, it will get automatically hidden after the AutoHideAfterShowDelay time interval has passed </summary>
        public bool AutoHideAfterShow;

        /// <summary> If AutoHideAfterShow is TRUE, this is the time interval after which this container will get automatically hidden </summary>
        public float AutoHideAfterShowDelay;

        /// <summary> If TRUE, when this container gets hidden, the GameObject this container component is attached to, will be disabled </summary>
        public bool DisableGameObjectWhenHidden;

        /// <summary> If TRUE, when this container gets hidden, the Canvas component found on the same GameObject this container component is attached to, will be disabled </summary>
        public bool DisableCanvasWhenHidden = true;

        /// <summary> If TRUE, when this container gets hidden, the GraphicRaycaster component found on the same GameObject this container component is attached to, will be disabled </summary>
        public bool DisableGraphicRaycasterWhenHidden = true;

        /// <summary> If TRUE, when this container is shown, any GameObject that is selected by the EventSystem.current will get deselected </summary>
        public bool ClearSelectedOnShow;

        /// <summary> If TRUE, when this container is hidden, any GameObject that is selected by the EventSystem.current will get deselected </summary>
        public bool ClearSelectedOnHide;

        /// <summary> If TRUE, after this container has been shown, the referenced selectable GameObject will get automatically selected by EventSystem.current </summary>
        public bool AutoSelectAfterShow;

        /// <summary> Reference to the GameObject that should be selected after this container has been shown. Works only if AutoSelectAfterShow is TRUE </summary>
        public GameObject AutoSelectTarget;

        /// <summary> Check if any Show animation is active (running) </summary>
        public bool anyShowAnimationIsActive => showReactions.Any(show => show.isActive);

        /// <summary> Check if any Hide animation is active (running) </summary>
        public bool anyHideAnimationIsActive => hideReactions.Any(hide => hide.isActive);

        /// <summary> Check if any Show or Hide animation is active (running) </summary>
        public bool anyAnimationIsActive => anyShowAnimationIsActive | anyHideAnimationIsActive;

        /// <summary> Check if there are any referenced Show progressors </summary>
        public bool hasShowProgressors => showProgressors.Any(p => p != null);

        /// <summary> Check if there are any referenced Hide progressors </summary>
        public bool hasHideProgressors => hideProgressors.Any(p => p != null);

        /// <summary> Check if there are any referenced ShowHide progressors </summary>
        public bool hasShowHideProgressors => showHideProgressors.Any(p => p != null);

        /// <summary> Check if there are any referenced progressors </summary>
        public bool hasProgressors => hasShowProgressors || hasHideProgressors || hasShowHideProgressors;

        /// <summary> Check if any referenced Show progressor is active (running) </summary>
        public bool anyShowProgressorIsActive => showProgressors.Where(p => p != null).Any(p => p.reaction.isActive);

        /// <summary> Check if any referenced Hide progressor is active (running) </summary>
        public bool anyHideProgressorIsActive => hideProgressors.Where(p => p != null).Any(p => p.reaction.isActive);

        /// <summary> Check if any referenced ShowHide progressor is active (running) </summary>
        public bool anyShowHideProgressorIsActive => showHideProgressors.Where(p => p != null).Any(p => p.reaction.isActive);

        /// <summary> Check if any referenced progressor is active (running) </summary>
        public bool anyProgressorIsActive => anyShowProgressorIsActive | anyHideProgressorIsActive | anyShowHideProgressorIsActive;

        private HashSet<Reaction> m_ShowReactions;
        /// <summary>
        /// Collection of reactions triggered by Show.
        /// <para/> This collection is dynamically generated at runtime.
        /// It is populated by all the animators controlled by this UIContainer.
        /// The animators automatically add/remove their reactions to/from this collection. 
        /// </summary>
        internal HashSet<Reaction> showReactions => m_ShowReactions ??= new HashSet<Reaction>();
        /// <summary>
        /// Get the maximum duration for the Show animations Max(startDelay) + Max(duration).
        /// <para> At start this value can be calculated only after 2 frames have passed (the time it takes for the reactions to register) </para>
        /// <para> For reactions that use random intervals for startDelay and/or duration, the maximum interval values are taken into account </para>
        /// </summary>
        public float totalDurationForShow => CalculateTotalShowDuration();

        private HashSet<Reaction> m_HideReactions;
        /// <summary>
        /// Collection of reactions triggered by Hide.
        /// <para/> This collection is dynamically generated at runtime.
        /// It is populated by all the animators controlled by this UIContainer.
        /// The animators automatically add/remove their reactions to/from this collection. 
        /// </summary>
        internal HashSet<Reaction> hideReactions => m_HideReactions ??= new HashSet<Reaction>();
        /// <summary>
        /// Get the maximum duration for the Hide animations Max(startDelay) + Max(duration).
        /// <para> At start this value can be calculated only after 2 frames have passed (the time it takes for the reactions to register) </para>
        /// <para> For reactions that use random intervals for startDelay and/or duration, the maximum interval values are taken into account </para>
        /// </summary>
        public float totalDurationForHide => CalculateTotalHideDuration();

        private Coroutine m_AutoHideCoroutine;
        private Coroutine m_CoroutineIsShowing;
        private Coroutine m_CoroutineIsHiding;
        private Coroutine m_DisableGameObjectWithDelayCoroutine;

        public UIContainer()
        {
            UseCustomStartPosition = true;

            OnShowCallback = new ModyEvent(nameof(OnShowCallback));
            OnVisibleCallback = new ModyEvent(nameof(OnVisibleCallback));
            OnHideCallback = new ModyEvent(nameof(OnHideCallback));
            OnHiddenCallback = new ModyEvent(nameof(OnHiddenCallback));
            OnVisibilityChangedCallback = new VisibilityStateEvent();
        }

        #if UNITY_EDITOR

        protected virtual void OnValidate()
        {
            if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) && !Application.isPlaying)
                CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }

        #endif // if UNITY_EDITOR

        #region ICanvasElement

        public virtual void Rebuild(CanvasUpdate executing) {}
        public virtual void LayoutComplete() {}
        public virtual void GraphicUpdateComplete() {}
        public bool IsDestroyed() => this == null;

        #endregion

        protected virtual void Awake()
        {
            if (!Application.isPlaying) return;
            BackButton.Initialize();

            m_Canvas = GetComponent<Canvas>();
            m_GraphicRaycaster = GetComponent<GraphicRaycaster>();
            hasCanvasGroup = canvasGroup != null;

            showReactions.Remove(null);
            hideReactions.Remove(null);

            executedFirstCommand = false;

            if (UseCustomStartPosition)
            {
                SetCustomStartPosition(CustomStartPosition);
            }
        }

        protected virtual void OnEnable()
        {
            if (!Application.isPlaying) return;
            hasCanvasGroup = canvasGroup != null;
            visibilityState = visibilityState;
        }

        protected virtual void Start()
        {
            if (!Application.isPlaying) return;
            RunBehaviour(OnStartBehaviour);
        }

        protected virtual void OnDisable()
        {
            if (!Application.isPlaying) return;
            StopIsShowingCoroutine();
            StopIsHidingCoroutine();

            showReactions.Remove(null);
            foreach (Reaction reaction in showReactions)
                reaction.Stop();

            hideReactions.Remove(null);
            foreach (Reaction reaction in hideReactions)
                reaction.Stop();
        }

        protected virtual void OnDestroy() {}

        public void SetCustomStartPosition(Vector3 startPosition, bool jumpToPosition = true)
        {
            CustomStartPosition = startPosition;

            showReactions.Remove(null);
            foreach (Reaction reaction in showReactions)
                if (reaction is UIMoveReaction moveReaction)
                    moveReaction.startPosition = startPosition;

            hideReactions.Remove(null);
            foreach (Reaction reaction in hideReactions)
                if (reaction is UIMoveReaction moveReaction)
                    moveReaction.startPosition = startPosition;

            if (jumpToPosition)
                rectTransform.anchoredPosition3D = startPosition;
        }

        private void ExecutedCommand(ShowHideExecute command)
        {
            showHideExecute?.Invoke(command);
            executedFirstCommand = true;
            previouslyExecutedCommand = command;

            if (!hasProgressors) return;

            showProgressors.RemoveNulls();
            hideProgressors.RemoveNulls();
            showHideProgressors.RemoveNulls();

            // ReSharper disable Unity.NoNullPropagation
            switch (command)
            {
                case ShowHideExecute.Show:
                    hideProgressors.ForEach(p => p.Stop());
                    showProgressors.ForEach(p => p.Play(PlayDirection.Forward));
                    showHideProgressors.ForEach(p => p.Play(PlayDirection.Forward));
                    break;
                case ShowHideExecute.Hide:
                    showProgressors.ForEach(p => p.Stop());
                    hideProgressors.ForEach(p => p.Play(PlayDirection.Forward));
                    showHideProgressors.ForEach(p => p.Play(PlayDirection.Reverse));
                    break;
                case ShowHideExecute.InstantShow:
                    hideProgressors.ForEach(p => p.Stop());
                    showProgressors.ForEach(p => p.SetProgressAtOne());
                    showHideProgressors.ForEach(p => p.SetProgressAtOne());
                    break;
                case ShowHideExecute.InstantHide:
                    showProgressors.ForEach(p => p.Stop());
                    hideProgressors.ForEach(p => p.SetProgressAtOne());
                    showHideProgressors.ForEach(p => p.SetProgressAtZero());
                    break;
                case ShowHideExecute.ReverseShow:
                    hideProgressors.ForEach(p => p.Stop());
                    showProgressors.ForEach(p => p.Reverse());
                    showHideProgressors.ForEach(p => p.Reverse());
                    break;
                case ShowHideExecute.ReverseHide:
                    showProgressors.ForEach(p => p.Stop());
                    hideProgressors.ForEach(p => p.Reverse());
                    showHideProgressors.ForEach(p => p.Reverse());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
            // ReSharper restore Unity.NoNullPropagation
        }

        #region Instant Show/Hide/Toggle

        public void InstantShow()
        {
            if (isVisible) return;

            StopIsShowingCoroutine();
            StopIsHidingCoroutine();

            canvas.enabled = true;           //enable the canvas
            graphicRaycaster.enabled = true; //enable the graphic raycaster
            if (hasCanvasGroup) canvasGroup.blocksRaycasts = graphicRaycaster.enabled;
            gameObject.SetActive(true); //set the active state to true (in case it has been disabled when hidden)

            ExecutedCommand(ShowHideExecute.InstantShow);

            if (ClearSelectedOnShow)
            {
                EventSystem.current.SetSelectedGameObject(null); //clear any selected
            }

            if (AutoSelectAfterShow && AutoSelectTarget != null) //check that the auto select option is enabled and that a GameObject has been referenced
            {
                EventSystem.current.SetSelectedGameObject(AutoSelectTarget); //select the referenced target
            }

            visibilityState = VisibilityState.IsShowing;
            visibilityState = VisibilityState.Visible;
        }

        public void InstantHide()
        {
            if (isHidden) return;

            StopIsShowingCoroutine();
            StopIsHidingCoroutine();

            ExecutedCommand(ShowHideExecute.InstantHide);

            if (ClearSelectedOnHide)
            {
                EventSystem.current.SetSelectedGameObject(null); //clear any selected
            }

            visibilityState = VisibilityState.IsHiding;
            visibilityState = VisibilityState.Hidden;
        }

        /// <summary>
        /// Toggles the visibility state.
        /// If Visible or IsShowing calls InstantHide.
        /// If Hidden or IsHiding calls InstantShow.
        /// </summary>
        public void InstantToggle()
        {
            switch (visibilityState)
            {
                case VisibilityState.Visible:
                case VisibilityState.IsShowing:
                    InstantHide();
                    break;
                case VisibilityState.Hidden:
                case VisibilityState.IsHiding:
                    InstantShow();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Animated Show/Hide/Toggle

        public void Show()
        {
            if (isShowing || isVisible) return;

            gameObject.SetActive(true); //set the active state to true (in case it has been disabled when hidden)

            if (m_LastFrameVisibilityStateChanged == Time.frameCount)
            {
                Coroutiner.ExecuteLater(Show, 2);
                return;
            }

            if (isHiding)
            {
                StopIsHidingCoroutine();
                ExecutedCommand(ShowHideExecute.ReverseHide);
                m_CoroutineIsShowing = StartCoroutine(IsShowing());
                return;
            }

            canvas.enabled = true; //enable the canvas
            if (DisableGraphicRaycasterWhenHidden)
            {
                graphicRaycaster.enabled = true; //enable the graphic raycaster
                if (hasCanvasGroup) canvasGroup.blocksRaycasts = graphicRaycaster.enabled;
            }

            ExecutedCommand(ShowHideExecute.Show);

            if (ClearSelectedOnShow)
            {
                EventSystem.current.SetSelectedGameObject(null); //clear any selected
            }

            if (AutoSelectAfterShow && AutoSelectTarget != null) //check that the auto select option is enabled and that a GameObject has been referenced
            {
                EventSystem.current.SetSelectedGameObject(AutoSelectTarget); //select the referenced target
            }

            m_CoroutineIsShowing = StartCoroutine(IsShowing());
        }

        private void StopIsShowingCoroutine()
        {
            if (m_CoroutineIsShowing == null) return;
            StopCoroutine(m_CoroutineIsShowing);
            m_CoroutineIsShowing = null;
        }

        private IEnumerator IsShowing()
        {
            StopIsHidingCoroutine();
            visibilityState = VisibilityState.IsShowing;
            yield return new WaitForEndOfFrame();
            
            while (anyAnimationIsActive) 
                yield return null;
            
            if (hasProgressors)
                while (anyProgressorIsActive)
                    yield return null;
            
            visibilityState = VisibilityState.Visible;
            m_CoroutineIsShowing = null;
        }

        public void Hide()
        {
            if (!isActiveAndEnabled) return;
            if (isHiding || isHidden) return;

            if (m_LastFrameVisibilityStateChanged == Time.frameCount)
            {
                Coroutiner.ExecuteLater(Hide, 2);
                return;
            }

            if (isShowing)
            {
                StopIsShowingCoroutine();
                ExecutedCommand(ShowHideExecute.ReverseShow);
                m_CoroutineIsHiding = StartCoroutine(IsHiding());
                return;
            }

            ExecutedCommand(ShowHideExecute.Hide);

            if (ClearSelectedOnHide)
            {
                EventSystem.current.SetSelectedGameObject(null); //clear any selected
            }

            m_CoroutineIsHiding = StartCoroutine(IsHiding());
        }

        private void StopIsHidingCoroutine()
        {
            StopDisableGameObject();
            if (m_CoroutineIsHiding == null) return;
            StopCoroutine(m_CoroutineIsHiding);
            m_CoroutineIsHiding = null;
        }

        private IEnumerator IsHiding()
        {
            StopDisableGameObject();
            StopIsShowingCoroutine();
            visibilityState = VisibilityState.IsHiding;
            yield return new WaitForEndOfFrame();
            
            while (anyAnimationIsActive)
                yield return null;
            
            if (hasProgressors)
                while (anyProgressorIsActive)
                    yield return null;
            
            visibilityState = VisibilityState.Hidden;
            m_CoroutineIsHiding = null;
        }

        /// <summary>
        /// Toggles the visibility state.
        /// If Visible or IsShowing calls Hide.
        /// If Hidden or IsHiding calls Show.
        /// </summary>
        public void Toggle()
        {
            switch (visibilityState)
            {
                case VisibilityState.Visible:
                case VisibilityState.IsShowing:
                    Hide();
                    break;
                case VisibilityState.Hidden:
                case VisibilityState.IsHiding:
                    Show();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        private void ExecuteOnShow()
        {
            OnShowCallback.Execute();
        }

        private void ExecuteOnHide()
        {
            OnHideCallback.Execute();
            StopAutoHide();
        }

        private void ExecuteOnVisible()
        {
            OnVisibleCallback.Execute();
            StartAutoHide();
        }

        private void ExecuteOnHidden()
        {
            OnHiddenCallback.Execute();
            canvas.enabled = !DisableCanvasWhenHidden;                     //disable the canvas, if the option is enabled
            graphicRaycaster.enabled = !DisableGraphicRaycasterWhenHidden; //disable the graphic raycaster, if the option is enabled
            if (hasCanvasGroup) canvasGroup.blocksRaycasts = graphicRaycaster.enabled;
            StartDisableGameObject();
        }

        private void StartDisableGameObject()
        {
            StopDisableGameObject();
            m_DisableGameObjectWithDelayCoroutine = StartCoroutine(DisableGameObjectWithDelay());
        }

        private void StopDisableGameObject()
        {
            if (m_DisableGameObjectWithDelayCoroutine == null)
                return;
            StopCoroutine(m_DisableGameObjectWithDelayCoroutine);
            m_DisableGameObjectWithDelayCoroutine = null;
        }

        private IEnumerator DisableGameObjectWithDelay()
        {
            //we need to wait for 3 frames to make sure all the connected animators have had enough time to initialize (it takes 2 frames for a position animator to get its start position from a layout group (THANKS UNITY!!!) FML)
            yield return null; //wait 1 frame (1 for the money)
            yield return null; //wait 1 frame (2 for the show)
            yield return null; //wait 1 frame (3 to get ready)
            // ...and 4 to f@#king go!
            gameObject.SetActive(!DisableGameObjectWhenHidden); //set the active state to false, if the option is enabled
        }

        private void StartAutoHide()
        {
            StopAutoHide();
            if (!AutoHideAfterShow) return;
            m_AutoHideCoroutine = StartCoroutine(AutoHideEnumerator());
        }

        private void StopAutoHide()
        {
            if (m_AutoHideCoroutine == null) return;
            StopCoroutine(m_AutoHideCoroutine);
            m_AutoHideCoroutine = null;
        }

        private IEnumerator AutoHideEnumerator()
        {
            yield return new WaitForSecondsRealtime(AutoHideAfterShowDelay);
            Hide();
            m_AutoHideCoroutine = null;
        }

        private void RunBehaviour(ContainerBehaviour behaviour)
        {
            switch (behaviour)
            {
                case ContainerBehaviour.Disabled:
                    //ignored
                    return;

                case ContainerBehaviour.InstantHide:
                    m_VisibilityState = VisibilityState.Visible;
                    InstantHide();
                    return;

                case ContainerBehaviour.InstantShow:
                    m_VisibilityState = VisibilityState.Hidden;
                    InstantShow();
                    return;

                case ContainerBehaviour.Hide:
                    m_VisibilityState = VisibilityState.Visible;
                    Hide();
                    return;

                case ContainerBehaviour.Show:
                    InstantHide();
                    Coroutiner.ExecuteLater(Show, 2);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
            }
        }

        private float CalculateTotalShowDuration()
        {
            float duration = CalculateTotalDurationForReactions(showReactions);
            float maxDelay = 0;
            float maxDuration = 0;

            showProgressors.RemoveNulls();
            foreach (FloatReaction r in showProgressors.Select(p => p.reaction))
            {
                maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            showHideProgressors.RemoveNulls();
            foreach (FloatReaction r in showHideProgressors.Select(p => p.reaction))
            {
                maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            return Mathf.Max(duration, maxDelay + maxDuration);
        }

        private float CalculateTotalHideDuration()
        {
            float duration = CalculateTotalDurationForReactions(hideReactions);
            float maxDelay = 0;
            float maxDuration = 0;

            hideProgressors.RemoveNulls();
            foreach (FloatReaction r in hideProgressors.Select(p => p.reaction))
            {
                maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            showHideProgressors.RemoveNulls();
            foreach (FloatReaction r in showHideProgressors.Select(p => p.reaction))
            {
                //don't calculate start delay as this progressor plays in reverse on hide
                // maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            return Mathf.Max(duration, maxDelay + maxDuration);
        }

        private static float CalculateTotalDurationForReactions(IEnumerable<Reaction> reactions, params Reaction[] others)
        {
            if (reactions == null) return 0f;
            float maxDelay = 0;
            float maxDuration = 0;
            foreach (Reaction r in reactions)
            {
                if (r == null) continue;
                maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            if (others == null)
                return maxDelay + maxDuration;

            foreach (Reaction r in others)
            {
                if (r == null) continue;
                maxDelay = Mathf.Max(maxDelay, r.settings.useRandomStartDelay ? r.settings.randomStartDelay.max : r.settings.startDelay);
                maxDuration = Mathf.Max(maxDuration, r.settings.useRandomDuration ? r.settings.randomDuration.max : r.settings.duration);
            }

            return maxDelay + maxDuration;
        }
    }
}
