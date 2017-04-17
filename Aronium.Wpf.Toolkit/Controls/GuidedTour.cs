﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Aronium.Wpf.Toolkit.Controls
{
    public class GuidedTour : Canvas, IDisposable
    {
        #region - Fields -

        private const double MARGIN = 0;
        private const double ANIMATION_MOVEMENT = 10;
        private const int ANIMATION_DURATION = 400;

        private int currentIndex = 0;
        private GuidedTourItem _currentItem;
        private Storyboard animateGuideStoryboard = new Storyboard();

        #endregion

        #region - Dependency properties -

        /// <summary>
        /// Identifies ItemsProperty dependency property.
        /// </summary>
        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(IList<GuidedTourItem>), typeof(GuidedTour));

        /// <summary>
        /// Identifies AnimateProperty dependency property.
        /// </summary>
        public static readonly DependencyProperty AnimateProperty = DependencyProperty.Register("Animate", typeof(bool), typeof(GuidedTour), new PropertyMetadata(true));

        /// <summary>
        /// Identifies ContextProperty dependency property.
        /// </summary>
        public static readonly DependencyProperty ContextProperty = DependencyProperty.Register("Context", typeof(string), typeof(GuidedTour));

        #endregion

        #region - Events -

        /// <summary>
        /// Identifies BeginEvent routed event.
        /// </summary>
        public static readonly RoutedEvent BeginEvent = EventManager.RegisterRoutedEvent("Begin", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GuidedTour));

        /// <summary>
        /// Identifies ClosingEvent routed event.
        /// </summary>
        public static readonly RoutedEvent ClosingEvent = EventManager.RegisterRoutedEvent("Closing", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GuidedTour));

        /// <summary>
        /// Identifies ClosedEvent routed event.
        /// </summary>
        public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent("Closed", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GuidedTour));

        /// <summary>
        /// Identifies FinishedEvent routed event.
        /// </summary>
        public static readonly RoutedEvent FinishedEvent = EventManager.RegisterRoutedEvent("Finished", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GuidedTour));

        #endregion

        #region - Constructors -

        /// <summary>
        /// Guide class static constructor.
        /// </summary>
        static GuidedTour()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GuidedTour), new FrameworkPropertyMetadata(typeof(GuidedTour)));
        }

        /// <summary>
        /// Initializes new instance of Guide class.
        /// </summary>
        public GuidedTour()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                Loaded += OnLoaded;
                SizeChanged += OnSizeChanged;
            }
        }

        #endregion

        #region - Private methods -

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            ShowGuideItem();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CurrentItem != null)
            {
                SetGuideItemPosition(CurrentItem);

                if (Animate)
                    CreateAnimation(CurrentItem);
            }
        }

        private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (CurrentItem != null)
                SetGuideItemPosition(CurrentItem);
        }

        private void ShowGuideItem(int index = 0)
        {
            if (Items != null && index < Items.Count())
            {
                currentIndex = index;

                if (Items.Any())
                {
                    // On first item, raise begin event notifying that guided tour has started
                    if (index == 0)
                    {
                        RaiseEvent(new RoutedEventArgs(BeginEvent));
                    }

                    var item = Items.ElementAt(index);

                    CurrentItem = item;

                    Children.Add(item);

                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        SetGuideItemPosition(item);

                        item.Show();

                        // Assign item completition eveents
                        if (item.Target is Button)
                            ((Button)item.Target).Click += OnGuideStepComplete;
                        else if (item.Target is TextBoxBase)
                        {
                            item.Target.KeyDown += OnGuideStepComplete;
                            ((TextBoxBase)item.Target).Focus();
                        }
                        else
                            item.Target.PreviewMouseDown += OnElementMouseDown;

                        item.Target.SizeChanged += OnTargetSizeChanged;
                        item.Target.IsVisibleChanged += OnTargetIsVisibleChanged;

                        var closeButton = item.Template.FindName("PART_ButtonClose", item) as Button;
                        if (closeButton != null)
                        {
                            // Remove any previously set listeners
                            closeButton.Click -= OnCloseButtonClick;

                            // Add close button click listener
                            closeButton.Click += OnCloseButtonClick;
                        }

                        if (Animate)
                        {
                            CreateAnimation(item);

                            item.MouseEnter += OnItemMouseEnter;
                            item.MouseLeave += OnItemMouseLeave;
                        }

                    }), DispatcherPriority.ContextIdle);
                }
            }
            else
            {
                CurrentItem = null;

                RemoveAnimation();

                RaiseEvent(new RoutedEventArgs(FinishedEvent));
            }
        }

        private void OnTargetIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Make sure that target guide item is hidden if target is not visible on the screen
            // Once target becomes visible again, show guide item again
            if ((bool)e.NewValue == false)
                GetGuideItem(sender as FrameworkElement).Hide();
            else
                GetGuideItem(sender as FrameworkElement).Show();
        }

        private void SetGuideItemPosition(GuidedTourItem item)
        {
            if (!IsLoaded) return;

            var targetPoint = item.Target.PointToScreen(new Point(0, 0));
            var thisPoint = this.PointToScreen(new Point(0, 0));

            switch (item.Placement)
            {
                case GuidedTourItem.ItemPlacement.Left:
                    item.Position = new Point((targetPoint.X - thisPoint.X) - item.ActualWidth - MARGIN, targetPoint.Y - thisPoint.Y + ((item.Target.ActualHeight / 2) - (item.ActualHeight / 2)));
                    break;
                case GuidedTourItem.ItemPlacement.Right:
                    item.Position = new Point((targetPoint.X - thisPoint.X) + item.Target.ActualWidth + MARGIN, targetPoint.Y - thisPoint.Y + ((item.Target.ActualHeight / 2) - (item.ActualHeight / 2)));
                    break;
                case GuidedTourItem.ItemPlacement.Bottom:
                    item.Position = new Point((targetPoint.X - thisPoint.X) + ((item.Target.ActualWidth / 2) - (item.ActualWidth / 2)),
                       targetPoint.Y - thisPoint.Y + (item.Target.ActualHeight + MARGIN));
                    break;
                case GuidedTourItem.ItemPlacement.Top:
                    item.Position = new Point((targetPoint.X - thisPoint.X) + ((item.Target.ActualWidth / 2) - (item.ActualWidth / 2)),
                       targetPoint.Y - thisPoint.Y - (item.ActualHeight + MARGIN));
                    break;
                default:
                    item.Position = new Point((targetPoint.X - thisPoint.X) + ((item.Target.ActualWidth / 2) - (item.ActualWidth / 2)), targetPoint.Y - thisPoint.Y + ((item.Target.ActualHeight / 2) - (item.ActualHeight / 2)));
                    break;
            }

            SetLeft(item, item.Position.X);
            SetTop(item, item.Position.Y);
        }

        private void CreateAnimation(GuidedTourItem item)
        {
            if (animateGuideStoryboard != null)
            {
                RemoveAnimation();
            }
            else
                animateGuideStoryboard = new Storyboard();

            double from = 0, to = 0;
            DoubleAnimation doubleAnimation = new DoubleAnimation();
            IEasingFunction easing = null; // new BackEase() { EasingMode = EasingMode.EaseIn };

            switch (item.Placement)
            {
                case GuidedTourItem.ItemPlacement.Left:
                case GuidedTourItem.ItemPlacement.Right:
                    to = item.Placement == GuidedTourItem.ItemPlacement.Right ? item.Position.X + ANIMATION_MOVEMENT : item.Position.X - ANIMATION_MOVEMENT;
                    from = item.Position.X;
                    Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(Canvas.Left)"));
                    break;
                case GuidedTourItem.ItemPlacement.Top:
                case GuidedTourItem.ItemPlacement.Bottom:
                    to = item.Placement == GuidedTourItem.ItemPlacement.Top ? item.Position.Y - ANIMATION_MOVEMENT : item.Position.Y + ANIMATION_MOVEMENT;
                    from = item.Position.Y;
                    Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("(Canvas.Top)"));
                    break;
            }

            doubleAnimation.From = from;
            doubleAnimation.To = to;
            doubleAnimation.Duration = new Duration(new TimeSpan(0, 0, 0, 0, ANIMATION_DURATION));
            doubleAnimation.BeginTime = TimeSpan.FromSeconds(0.3);
            doubleAnimation.AutoReverse = true;
            Storyboard.SetTarget(doubleAnimation, item);
            animateGuideStoryboard.Children.Add(doubleAnimation);
            doubleAnimation.RepeatBehavior = RepeatBehavior.Forever;
            doubleAnimation.EasingFunction = easing;
            animateGuideStoryboard.Begin();
        }

        private void RemoveGuideItem(object targetElement)
        {
            var guideItem = GetGuideItem(targetElement as FrameworkElement);

            guideItem.Visibility = Visibility.Hidden;

            var target = ((FrameworkElement)targetElement);

            if (target is Button)
                ((Button)target).Click -= OnGuideStepComplete;
            else if (target is TextBoxBase)
                target.KeyDown -= OnGuideStepComplete;
            else
                target.PreviewMouseDown -= OnElementMouseDown;
            target.SizeChanged -= OnTargetSizeChanged;

            guideItem.MouseEnter -= OnItemMouseEnter;
            guideItem.MouseLeave -= OnItemMouseLeave;

            Children.Remove(guideItem);
        }

        private void RemoveAnimation()
        {
            if (animateGuideStoryboard == null) return;

            animateGuideStoryboard.Stop();
            animateGuideStoryboard.Remove();
            animateGuideStoryboard.Children.Clear();
            animateGuideStoryboard.Remove();
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            // Create new closing event args instance
            var ea = new ClosingGuidedTourEventArgs(ClosingEvent);

            // Raise closing event
            RaiseEvent(ea);

            // If closing was canceled, return
            if (ea.Cancel)
                return;

            IsCanceled = true;

            animateGuideStoryboard.Stop();
            animateGuideStoryboard = null;

            RemoveGuideItem(CurrentItem.Target);

            RaiseEvent(new RoutedEventArgs(ClosedEvent, CurrentItem));
        }

        private void OnGuideStepComplete(object sender, RoutedEventArgs e)
        {
            OnGuideItemTargetSelected(sender);
        }

        private void OnElementMouseDown(object sender, MouseButtonEventArgs e)
        {
            OnGuideItemTargetSelected(sender);
        }

        private void OnGuideItemTargetSelected(object sender)
        {
            RemoveGuideItem(sender);

            currentIndex++;

            ShowGuideItem(currentIndex);
        }

        private void OnItemMouseLeave(object sender, MouseEventArgs e)
        {
            if (animateGuideStoryboard != null)
                animateGuideStoryboard.Resume();
        }

        private void OnItemMouseEnter(object sender, MouseEventArgs e)
        {
            animateGuideStoryboard.Pause();
        }

        private GuidedTourItem GetGuideItem(FrameworkElement target)
        {
            return Items.First(x => x.Target == target);
        }

        #endregion

        #region - Properties -

        #region " Events "

        /// <summary>
        /// Occurs when guided tour has started.
        /// </summary>
        public event RoutedEventHandler Begin
        {
            add { AddHandler(BeginEvent, value); }
            remove { RemoveHandler(BeginEvent, value); }
        }

        /// <summary>
        /// Occurs before guided tour is closed.
        /// </summary>
        public event RoutedEventHandler Closing
        {
            add { AddHandler(ClosingEvent, value); }
            remove { RemoveHandler(ClosingEvent, value); }
        }

        /// <summary>
        /// Occurs when guided tour is closed.
        /// </summary>
        public event RoutedEventHandler Closed
        {
            add { AddHandler(ClosedEvent, value); }
            remove { RemoveHandler(ClosedEvent, value); }
        }

        /// <summary>
        /// Occurs when guided tour is finished.
        /// </summary>
        public event RoutedEventHandler Finished
        {
            add { AddHandler(FinishedEvent, value); }
            remove { RemoveHandler(FinishedEvent, value); }
        } 

        #endregion

        /// <summary>
        /// Gets or sets a value indicating whether guided tour was canceled.
        /// </summary>
        public bool IsCanceled { get; set; }

        /// <summary>
        /// Gets or sets guide items.
        /// </summary>
        public IList<GuidedTourItem> Items
        {
            get { return (IList<GuidedTourItem>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether guide items should be animated.
        /// </summary>
        public bool Animate
        {
            get { return (bool)GetValue(AnimateProperty); }
            set { SetValue(AnimateProperty, value); }
        }

        /// <summary>
        /// Gets or sets distinctive context used for this guided tour.
        /// </summary>
        public string Context
        {
            get { return (string)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        /// <summary>
        /// Gets current guide item.
        /// </summary>
        public GuidedTourItem CurrentItem
        {
            get
            {
                return _currentItem;
            }
            private set
            {
                _currentItem = value;
            }
        }

        #endregion

        #region - Public methods -

        /// <summary>
        /// Restarts guided tour.
        /// </summary>
        public void Reset()
        {
            IsCanceled = false;

            if (CurrentItem != null)
                RemoveGuideItem(CurrentItem.Target);

            ShowGuideItem();
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public void Dispose()
        {
            CurrentItem = null;
            RemoveAnimation();
        }

        #endregion
    }

    public class ClosingGuidedTourEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes new instance of ClosingGuidedTourEventArgs with specified routed event.
        /// </summary>
        /// <param name="routedEvent">Routed event.</param>
        public ClosingGuidedTourEventArgs(RoutedEvent routedEvent) : base(routedEvent) { }

        /// <summary>
        /// Gets or sets a value indicating whether closing action was canceled.
        /// </summary>
        public bool Cancel { get; set; }
    }
}
