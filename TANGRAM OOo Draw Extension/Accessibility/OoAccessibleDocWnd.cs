﻿using System;
using System.Collections.Generic;
using tud.mci.tangram.controller;
using tud.mci.tangram.controller.observer;
using tud.mci.tangram.models.Interfaces;
using tud.mci.tangram.util;
using unoidl.com.sun.star.accessibility;
using unoidl.com.sun.star.awt;
using unoidl.com.sun.star.drawing;
using unoidl.com.sun.star.frame;
using unoidl.com.sun.star.lang;
using unoidl.com.sun.star.view;
using System.Threading;
using System.Threading.Tasks;

namespace tud.mci.tangram.Accessibility
{
    /// <summary>
    /// Encapsulates important properties of a document and its surrounding window elements.
    /// </summary>
    public partial class OoAccessibleDocWnd : IUpdateable, IDisposable, IDisposingObserver
    {
        #region Member

        /// <summary>
        /// The main window of the document. 
        /// </summary>
        public readonly XAccessible MainWindow;
        /// <summary>
        /// The document component containing the document objects.
        /// </summary>
        public XAccessible Document { get; private set; }
        /// <summary>
        /// the parent window/frame containing the document and is child of the main window 
        /// </summary>
        public XAccessible DocumentWindow { get; private set; }
        /// <summary>
        /// The window handle of the main window
        /// </summary>
        public IntPtr Whnd { get; private set; }
        /// <summary>
        /// The process ID of the current OpenOffice instance
        /// </summary>
        public readonly int ProcessId = OoUtils.GetOoProcessID();

        /// <summary>
        /// The OoAccComponent for the documents. allows of collision testing etc.
        /// </summary>
        public OoAccComponent DocumentComponent
        {
            get
            {
                try
                {
                    var doc = new OoAccComponent(Document as XAccessibleComponent);
                    if (doc.Role == AccessibleRole.INVALID)
                    {
                        Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] document component invalid");

                        if (MainWindow != null)
                        {
                            setDocumentAndDocumentWindow(MainWindow);
                            doc = new OoAccComponent(Document as XAccessibleComponent);
                            if (doc.Role == AccessibleRole.INVALID)
                            {
                                Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] document component invalid even after reset");
                            }
                        }
                        else
                        {
                            // document seems to be invalid ore already disposed
                            Dispose();
                        }
                    }
                    else if (doc.Role == AccessibleRole.DISPOSED)
                    {
                        Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] document component seems to be already disposed");
                        Dispose();
                    }
                    return doc;
                }
                catch (unoidl.com.sun.star.lang.DisposedException)
                {
                    Dispose();
                }
                return null;
            }
        }

        private string _lastTitle = String.Empty;
        /// <summary>
        /// The title of the document
        /// </summary>
        public String Title
        {
            get
            {
                string t = getTitle();
                if (String.IsNullOrEmpty(t))
                {
                    t = _lastTitle;
                }
                else
                {
                    _lastTitle = t;
                }
                return t;
            }
        }

        XDrawPagesSupplier _dps = null;
        /// <summary>
        /// The corresponding DOM object for the DRAW document to create pages
        /// </summary>
        public XDrawPagesSupplier DrawPageSupplier
        {
            get { return _dps; }
            set
            {
                _dps = value;
                _lastController = null;
                _lastController = Controller;

                // set the meta data access
                MetaData = new DrawDocMetaDataSet(_dps as unoidl.com.sun.star.document.XDocumentPropertiesSupplier);
            }
        }
        /// <summary>
        /// The corresponding OodrawPagesObserver for this XDrawPagesSupplier
        /// </summary>
        public OoDrawPagesObserver DrawPagesObs;

        #region EventListener Forwarder
        readonly XWindowListener2_Forwarder xWindowListener = new XWindowListener2_Forwarder();
        readonly XSelectionListener_Forwarder xSelectionListener = new XSelectionListener_Forwarder();
        readonly XAccessibleEventListener_Forwarder xAccessibleListener = new XAccessibleEventListener_Forwarder();
        #endregion

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="OoAccessibleDocWnd"/> struct.
        /// </summary>
        /// <param name="mainWnd">The main window (top Window).</param>
        /// <param name="doc">The DOCUMENT (role) object.</param>
        public OoAccessibleDocWnd(XAccessible mainWnd, XAccessible doc)
        {
            registerToEventForwarder();
            DrawPageSupplier = null;
            Whnd = IntPtr.Zero;
            MainWindow = mainWnd;
            Document = doc;
            setDocumentAndDocumentWindow(doc);

            if (MainWindow != null)
            {
                Whnd = OoUtils.GetWindowHandle(MainWindow as XSystemDependentWindowPeer);
            }

            // Main Window Events
            addWindowListeners(MainWindow);

            Task t = new Task(
                () =>
                {
                    int trys = 0;
                    while (!getXDrawPageSupplier() && ++trys < 10)
                    {
                        Thread.Sleep(1000);
                    }

                    //addSelectionListener(DrawPageSupplier);
                }
                );
            t.Start();


        }

        /// <summary>
        /// NOT IMPLEMENTED YET
        /// </summary>
        public void Update() { }

        #region private initialization functions

        void registerToEventForwarder()
        {
            if (xWindowListener != null)
            {
                xWindowListener.Disposing += new EventHandler<EventObjectForwarder>(disposing);
                xWindowListener.WindowDisabled += new EventHandler<EventObjectForwarder>(xWindowListener_WindowDisabled);
                xWindowListener.WindowEnabled += new EventHandler<EventObjectForwarder>(xWindowListener_WindowEnabled);
                xWindowListener.WindowHidden += new EventHandler<EventObjectForwarder>(xWindowListener_WindowHidden);
                xWindowListener.WindowMoved += new EventHandler<WindowEventForwarder>(xWindowListener_WindowMoved);
                xWindowListener.WindowResized += new EventHandler<WindowEventForwarder>(xWindowListener_WindowResized);
                xWindowListener.WindowShown += new EventHandler<EventObjectForwarder>(xWindowListener_WindowShown);
            }
            else
            {
                Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] xWindowListener_Forwarder is null");
            }

            if (xAccessibleListener != null)
            {
                xAccessibleListener.Disposing += new EventHandler<EventObjectForwarder>(disposing);
                xAccessibleListener.NotifyEvent += new EventHandler<AccessibleEventObjectForwarder>(xAccessibleListener_NotifyEvent);
            }
            else
            {
                Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] xAccessibleEvent_Forwarder is null");
            }

            if (xSelectionListener != null)
            {
                xSelectionListener.Disposing += new EventHandler<EventObjectForwarder>(disposing);
                xSelectionListener.SelectionChanged += new EventHandler<EventObjectForwarder>(xSelectionListener_SelectionChanged);
            }
            else
            {
                Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] xWSelectionListener_Forwarder is null");
            }

        }

        private void setDocumentAndDocumentWindow(XAccessible doc)
        {
            if (doc == null)
            {
                doc = OoAccessibility.IsDrawWindow(MainWindow) as XAccessible;
            }
            if (doc != null && doc is XAccessibleContext)
            {
                var par = ((XAccessibleContext)doc).getAccessibleParent();
                while (par != null && !(par is XWindow2))
                {
                    ((XAccessibleContext)par).getAccessibleParent();
                }
                DocumentWindow = par as XAccessible;
            }
            else
            {
                DocumentWindow = null;
            }

            //Document Window Events
            addWindowListeners(DocumentWindow);

            //Document Events
            addDocumentListeners(Document);
        }

        private String getTitle()
        {
            String title = String.Empty;

            if (DrawPagesObs != null)
            {
                title = DrawPagesObs.Title;
            }

            if (String.IsNullOrWhiteSpace(title))
            {

                String docT = String.Empty;
                if (Document != null && Document.getAccessibleContext() != null)
                {
                    docT = OoAccessibility.GetAccessibleName(Document);
                }
                if (!String.IsNullOrEmpty(docT))
                {
                    docT = docT.Substring(0, Math.Max(0, docT.LastIndexOf('-'))).Trim();
                    title = docT;
                }

                String mainWndT = String.Empty;

                if (MainWindow != null && MainWindow.getAccessibleContext() != null)
                {
                    mainWndT = OoAccessibility.GetAccessibleName(MainWindow);
                }

                if (!String.IsNullOrEmpty(mainWndT)) //TODO: check why this is empty on CO2-Labels and unbenannt
                {
                    mainWndT = mainWndT.Substring(0,
                            mainWndT.LastIndexOf('.') > 0 ?
                                mainWndT.LastIndexOf('.') : Math.Max(0, docT.LastIndexOf('-'))
                            ).Trim();
                    title = mainWndT;
                }

                if (mainWndT.Equals(docT))
                    title = docT;
                else
                    Logger.Instance.Log(LogPriority.IMPORTANT, this, "Cannot get correct title of the Oo document: Window title: '" + mainWndT + "' Document title: '" + docT + "'");
            }
            return title;
        }

        private void addWindowListeners(object p)
        {
            if (p != null && p is XWindow2)
            {
                try { ((XWindow2)p).addWindowListener(xWindowListener); }
                catch
                {
                    Logger.Instance.Log(LogPriority.DEBUG, this, "can't add window listener");
                    try { ((XWindow2)p).removeWindowListener(xWindowListener); }
                    catch
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(5);
                            ((XWindow2)p).removeWindowListener(xWindowListener);
                        }
                        catch (Exception e) { Logger.Instance.Log(LogPriority.ALWAYS, this, "can't remove window listener", e); }
                    }
                    try { ((XWindow2)p).addWindowListener(xWindowListener); }
                    catch (Exception e)
                    {
                        Logger.Instance.Log(LogPriority.ALWAYS, this, "finaly can't add window listener", e);
                    }
                }
            }
        }

        private void addDocumentListeners(object dw)
        {


            if (xAccessibleListener != null)
            {
                if (dw != null)
                {
                    if (dw is XAccessibleEventBroadcaster)
                    {
                        Logger.Instance.Log(LogPriority.DEBUG, this, "Add Accessible event listener to: " + OoAccessibility.PrintAccessibleInfos(dw as XAccessible));
                        try
                        {
#if LIBRE
                            ((XAccessibleEventBroadcaster)dw).removeAccessibleEventListener(xAccessibleListener);
#else
                            ((XAccessibleEventBroadcaster)dw).removeEventListener(xAccessibleListener);
#endif

                        }
                        catch (unoidl.com.sun.star.lang.DisposedException)
                        {
                            this.Dispose();
                            return;
                        }
                        catch
                        {
                            try
                            {
#if LIBRE
                                ((XAccessibleEventBroadcaster)dw).removeAccessibleEventListener(xAccessibleListener);
#else
                                ((XAccessibleEventBroadcaster)dw).removeEventListener(xAccessibleListener);
#endif
                            }
                            catch (unoidl.com.sun.star.lang.DisposedException) { Dispose(); return; }
                            catch (Exception e) { Logger.Instance.Log(LogPriority.ALWAYS, this, "can't remove document listener", e); }
                        }

                        try
                        {
#if LIBRE
                            ((XAccessibleEventBroadcaster)dw).addAccessibleEventListener(xAccessibleListener);
#else
                            ((XAccessibleEventBroadcaster)dw).addEventListener(xAccessibleListener);
#endif
                        }
                        catch (unoidl.com.sun.star.lang.DisposedException) { Dispose(); return; }
                        catch (System.Exception e)
                        {
                            Logger.Instance.Log(LogPriority.ALWAYS, this, "can't add document listener");
                        }
                    }
                }
            }
        }

        private void addSelectionListener(Object dps)
        {
            /*
             * The document listener only throws selection events after 
             * editing the document the first time. Until this, in loaded 
             * drawing, no selections were reported through modify events.
             * The original selection event from the XSelectionSupplier 
             * has to been used for this. 
             */

            if (dps != null && dps is XModel2)
            {
                try
                {
                    var controller = ((XModel2)dps).getCurrentController();
                    if (controller != null && controller is XSelectionSupplier)
                    {
                        ((XSelectionSupplier)controller).addSelectionChangeListener(xSelectionListener);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] Can't add selection listener to document window: " + ex);
                }
            }
        }

        /// <summary>
        /// Try to find the corresponding XDrawPageSupplier to the given Window.
        /// </summary>
        private bool getXDrawPageSupplier()
        {
            List<XDrawPagesSupplier> dps = OoDrawUtils.GetDrawPageSuppliers(OO.GetDesktop());
            if (dps.Count > 0)
            {
                foreach (var item in dps)
                {
                    //string runtimeID = OoUtils.GetStringProperty(item, "RuntimeUID");
                    //string buildID = OoUtils.GetStringProperty(item, "BuildId");

                    XAccessible acc = OoDrawUtils.GetXAccessibleFromDrawPagesSupplier(item);
                    if (acc != null)
                    {
                        var root = OoAccessibility.GetRootPaneFromElement(acc);
                        if (root != null)
                        {
                            if (root.Equals(MainWindow))
                            {
                                Logger.Instance.Log(LogPriority.DEBUG, this, "XDrawPageSuppliere found!!");
                                DrawPageSupplier = item;

                                //prepareForSelection(item); // Bad hack for getting accessible selection events

                                DrawPagesObs = new OoDrawPagesObserver(DrawPageSupplier, DocumentComponent, this);
                                return true;
                            }
                            else
                            {
                                Logger.Instance.Log(LogPriority.OFTEN, this, "[ERROR] - Can't find root element from DrawPagesSupplier (root is not MainWindow)");
                            }
                        }
                        else
                        {
                            Logger.Instance.Log(LogPriority.OFTEN, this, "[ERROR] - Can't find root element from DrawPagesSupplier (root is null)");
                        }
                    }
                    else
                    {
                        Logger.Instance.Log(LogPriority.OFTEN, this, "[ERROR] - Can't find XAccessible for DrawPagesSupplier (root is null)");
                    }
                }
            }
            Logger.Instance.Log(LogPriority.ALWAYS, this, "[FATAL ERROR] cannot find XDrawPageSuppliere for window");
            return false;
        }

        /// <summary>
        /// Hack to activate the accessibility events for selection.
        /// </summary>
        /// <param name="item">The item.</param>
        private void prepareForSelection(XDrawPagesSupplier item)
        {
            // adding and deleting one shape to the document so the selections 
            // will be activated for notify events of the XAccessibleEventBroadcaster
            // FIXME: remove this hack - if the event works properly

            if (item != null)
            {

                TimeLimitExecutor.ExecuteWithTimeLimit(
                        () =>
                        {
                            var shape = OoDrawUtils.CreateEllipseShape(item, 0, 0, 3000, 3000);
                            var page = OoDrawUtils.GetCurrentPage(item);
                            if (page != null && shape != null)
                            {
                                OoDrawUtils.AddShapeToDrawPage(shape, page);
                                OoDrawUtils.RemoveShapeFromDrawPage(shape, page);
                            }
                        }
                    );
            }
            else
            {
                Logger.Instance.Log(LogPriority.IMPORTANT, this, "[FATAL ERROR] Cannot add shapes for activating selection events");
            }
        }

        #endregion

        #region EventListeners

        void disposing(object sender, EventObjectForwarder e) { disposing(e.E); }

        #region XWindowListener2

        void xWindowListener_WindowShown(object sender, EventObjectForwarder e) { windowShown(e.E); }
        void xWindowListener_WindowResized(object sender, WindowEventForwarder e) { windowResized(e.E); }
        void xWindowListener_WindowMoved(object sender, WindowEventForwarder e) { windowMoved(e.E); }
        void xWindowListener_WindowHidden(object sender, EventObjectForwarder e) { windowHidden(e.E); }
        void xWindowListener_WindowEnabled(object sender, EventObjectForwarder e) { windowEnabled(e.E); }
        void xWindowListener_WindowDisabled(object sender, EventObjectForwarder e) { windowDisabled(e.E); }

        void windowDisabled(unoidl.com.sun.star.lang.EventObject e) { fireWindowEvent(WindowEventType.disabled, e); }
        void windowEnabled(unoidl.com.sun.star.lang.EventObject e) { fireWindowEvent(WindowEventType.enabled, e); }
        void windowHidden(unoidl.com.sun.star.lang.EventObject e) { fireWindowEvent(WindowEventType.hidden, e); }
        void windowMoved(WindowEvent e) { fireWindowEvent(WindowEventType.moved, e); }
        void windowResized(WindowEvent e) { fireWindowEvent(WindowEventType.rezized, e); }
        void windowShown(unoidl.com.sun.star.lang.EventObject e) { fireWindowEvent(WindowEventType.shown, e); }

        void disposing(unoidl.com.sun.star.lang.EventObject Source) { fireWindowEvent(WindowEventType.disposing, Source); }

        #endregion

        #region XAccessibleEventListener

        void xAccessibleListener_NotifyEvent(object sender, AccessibleEventObjectForwarder e) { notifyEvent(e.E); }

        void notifyEvent(AccessibleEventObject aEvent)
        {
            Logger.Instance.Log(LogPriority.DEBUG, this, "[ACCESSIBLE] accessible event in document " + (aEvent != null ? OoAccessibility.GetAccessibleEventIdFromShort(aEvent.EventId).ToString() : ""));

            /* you can not rely on the selection events. 
             * They will only be thrown after editing the document. So selections in 
             * all loaded document will not been reported ever! 
             * Use the XSelectionProveider of the XControllen instead!
             */
            fireAccessibleEvent(aEvent);
        }

        #endregion

        #region XSelectionChangeListener

        void xSelectionListener_SelectionChanged(object sender, EventObjectForwarder e)
        {
            selectionChanged(e.E);
        }

        /// <summary>
        /// Selections the changed.
        /// Prepare this event that it looks like the accessible modify selection event.
        /// The selection supplier returns the real XShapes!!! and not the XAccessible objects :-/
        /// </summary>
        /// <param name="aEvent">A event.</param>
        void selectionChanged(EventObject aEvent)
        {
            Logger.Instance.Log(LogPriority.DEBUG, this, "[SELECTION] selection changed event in document " + Title);
            //System.Diagnostics.Debug.WriteLine("## AccessibleDocWnd --> selction changed (Selection Listener)");
            if (aEvent != null && aEvent.Source != null)
            {
                fireSelectionEvent(new OoSelectionChandedEventArgs(aEvent.Source));
            }
        }

        #endregion

        #endregion

        #region Events

        /// <summary>
        /// Event forwarder for events from the two observed windows (Main window and Document window)
        /// </summary>
        public event EventHandler<OoAccessibleDocWindowEventArgs> WindowEvent;
        /// <summary>
        /// Forwarder for accessibility events of the observed Document. 
        /// </summary>
        public event EventHandler<OoAccessibleDocAccessibleEventArgs> AccessibleEvent;

        public event EventHandler<OoSelectionChandedEventArgs> SelectionEvent;

        void fireWindowEvent(WindowEventType type, EventObject e)
        {
            if (WindowEvent != null)
            {
                try
                {
                    WindowEvent.Invoke(this, new OoAccessibleDocWindowEventArgs(type, e));
                }
                catch (Exception ex) { Logger.Instance.Log(LogPriority.DEBUG, this, "cant fire window event", ex); }
            }
        }

        void fireAccessibleEvent(AccessibleEventObject aEvents)
        {
            if (AccessibleEvent != null)
            {
                try
                {
                    AccessibleEvent.Invoke(this, new OoAccessibleDocAccessibleEventArgs(aEvents));
                }
                catch (Exception ex) { Logger.Instance.Log(LogPriority.DEBUG, this, "can't fire accessibility event", ex); }
            }
        }

        void fireSelectionEvent(OoSelectionChandedEventArgs aEvents)
        {
            if (SelectionEvent != null)
            {
                try
                {
                    SelectionEvent.Invoke(this, aEvents);
                }
                catch (Exception ex) { Logger.Instance.Log(LogPriority.DEBUG, this, "cant fire selection event", ex); }
            }
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "OoAccessibleDocWnd [" + GetHashCode() + "] "
                + "Title:'" + Title
                + "' wHndl:" + Whnd
                + " MainWnd hash:" + (MainWindow != null ? MainWindow.GetHashCode().ToString() : "null")
                + " DocWnd hash:" + (DocumentWindow != null ? DocumentWindow.GetHashCode().ToString() : "null")
                + " Doc hash:" + (Document != null ? Document.GetHashCode().ToString() : "null");
        }

        /// <summary>
        /// Gets the registered shape observer to the accessible component if it is already registered.
        /// </summary>
        /// <param name="xAccessibleComponent">The x accessible component.</param>
        /// <returns>an accessible component if it is already registered</returns>
        internal OoShapeObserver GetRegisteredShapeObserver(XAccessibleComponent xAccessibleComponent)
        {
            OoShapeObserver shape = DrawPagesObs.GetRegisteredShapeObserver(xAccessibleComponent);
            return shape;
        }

        /// <summary>
        /// Gets the registered shape observer to the accessible component if it is already registered.
        /// </summary>
        /// <param name="xAccessibleComponent">The x accessible component.</param>
        /// <returns>an accessible component if it is already registered</returns>
        public OoShapeObserver GetRegisteredShapeObserver(OoAccComponent accessibleComponent)
        {
            return GetRegisteredShapeObserver(accessibleComponent.AccComp);
        }

        /// <summary>
        /// Return the current active page.
        /// Trys to call the active page observer only once
        /// </summary>
        /// <returns></returns>
        public OoDrawPageObserver GetActivePage()
        {
            int i = 0;
            while (_runing && ++i < 10)
            {
                Thread.Sleep(50);
            }

            if (!_runing)
            {
                OoDrawPageObserver pObs = GetActivePageObserver();
                if (pObs != null)
                {
                    return pObs;
                }
            }
            return null;
        }


        volatile bool _runing = false;
        /// <summary>
        /// Return the observer of the current active page.
        /// </summary>
        /// <returns></returns>
        public OoDrawPageObserver GetActivePageObserver()
        {
            _runing = true;
            OoDrawPageObserver pageObs = null;
            if (DrawPagesObs != null && DrawPagesObs.HasDrawPages()
                && DrawPageSupplier != null && DrawPageSupplier is XModel2
                )
            {
                var dps = DrawPagesObs.GetDrawPages();
                if (dps.Count > 1) // if only one page is known - take it
                {
                    // get the controller of the Draw application
                    XController contr = Controller;

                    int pid = getCurrentActivePageId(contr);

                    // find the OoDrawPageObserver for the correct page number
                    if (pid > 0)
                    {
                        foreach (var page in DrawPagesObs.GetDrawPages())
                        {
                            if (page != null && page.DrawPage != null)
                            {
                                int pNum = util.OoUtils.GetIntProperty(page.DrawPage, "Number");
                                if (pNum == pid)
                                {
                                    pageObs = page;
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    pageObs = dps[0];
                }
            }
            _runing = false;
            return pageObs;
        }


        XController _lastController = null;
        volatile bool _gttingController = false;
        /// <summary>
        /// Tries the get the controller of the document model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public XController Controller
        {
            get
            {
                if (_lastController != null) return _lastController;
                if (_gttingController) return _lastController;

                XController contr = null;
                //lock (_controlerLock)
                {
                    _gttingController = true;
                    XModel2 model = DrawPageSupplier as XModel2;
                    if (model != null)
                    {
                        //FIXME: lead to hang ons
                        //TimeLimitExecutor.WaitForExecuteWithTimeLimit(50, () => { model.lockControllers(); }, "LockControlers");
                        TimeLimitExecutor.WaitForExecuteWithTimeLimit(200,
                                    () =>
                                    {
                                        try
                                        {
                                            if (model != null) contr = model.getCurrentController();
                                        }
                                        catch (DisposedException ex)
                                        {
                                            Dispose();
                                            Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] controller for document window already disposed: " + ex);
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] can't get controller for document window: " + ex);
                                        }
                                    }
                                    , "getControllerOfModel"
                                    );
                        //FIXME: lead to hang ons
                        //TimeLimitExecutor.ExecuteWithTimeLimit(50, () => { model.unlockControllers(); }, "UnlockControlers");
                    }
                    _lastController = contr;

                    if (_lastController is XSelectionSupplier)
                    {
                        try
                        {
                            XSelectionSupplier supl = _lastController as XSelectionSupplier;
                            if (supl != null)
                            {
                                try { supl.removeSelectionChangeListener(xSelectionListener); }
                                catch { }
                                try
                                {
                                    supl.addSelectionChangeListener(xSelectionListener);
                                }
                                catch
                                {
                                    System.Threading.Thread.Sleep(5);
                                    try
                                    {
                                        supl.addSelectionChangeListener(xSelectionListener);
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Logger.Instance.Log(LogPriority.DEBUG, this, "[ERROR] Could not add selection listener to documents' controler");
                        }
                    }

                    _gttingController = false;
                }
                return contr;
            }
        }

        private readonly object _controlerLock = new Object();
        /// <summary>
        /// Try to get the page number of the current active page
        /// </summary>
        /// <param name="contr"></param>
        /// <param name="pid"></param>
        /// <returns></returns>
        private int getCurrentActivePageId(XController contr)
        {
            int pid = -1;
            lock (_controlerLock)
            {
                TimeLimitExecutor.WaitForExecuteWithTimeLimit(500,
                         () =>
                         {
                             try
                             {
                                 if (contr != null && contr is XDrawView)
                                 {
                                     // get the current page
                                     var page = ((XDrawView)contr).getCurrentPage();
                                     // get the number
                                     pid = util.OoUtils.GetIntProperty(page, "Number");
                                 }
                             }
                             catch (DisposedException)
                             {
                                 Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] Controller seems to be already disposed");
                                 this.Dispose();
                             }
                             catch (System.Exception ex)
                             {
                                 Logger.Instance.Log(LogPriority.IMPORTANT, this, "[ERROR] cant use the controller of document window: " + ex);
                             }
                         }
                         , "useControllerOfModel"
                         );
            }
            return pid;
        }


        #region IDisposable

        public void Dispose()
        {
            Logger.Instance.Log(LogPriority.MIDDLE, this, "[INFO]" + Whnd + " window closed and Observer disposing");
            Disposed = true;
            fire_DisposingEvent();
            OO.CheckConnection();
        }
        #endregion

        #region IDisposingObserver

        /// <summary>
        /// Gets a value indicating whether this <see cref="AbstractDisposingBase"/> is disposed.
        /// </summary>
        /// <value><c>true</c> if disposed; otherwise, <c>false</c>.</value>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Occurs when this object is disposing.
        /// </summary>
        public event EventHandler ObserverDisposing;

        /// <summary>
        /// Fires the disposing event.
        /// </summary>
        protected virtual void fire_DisposingEvent()
        {
            if (ObserverDisposing != null)
            {
                try
                {
                    ObserverDisposing.Invoke(this, null);
                }
                catch { }
            }
        }

        #endregion


    }

    #region Event Arg Definition

    public class OoAccessibleDocWindowEventArgs : EventArgs
    {
        /// <summary>
        /// The type of the window event
        /// </summary>
        public readonly WindowEventType Type;
        /// <summary>
        /// The original native event object (Can be EventObject or WindowEvent)
        /// </summary>
        public readonly EventObject E;

        /// <summary>
        /// Initializes a new instance of the <see cref="OoAccessibleDocWindowEventArgs"/> class.
        /// </summary>
        /// <param name="type">The original event type.</param>
        /// <param name="e">The native event object.</param>
        public OoAccessibleDocWindowEventArgs(WindowEventType type, EventObject e)
        {
            Type = type;
            E = e;
        }

    }

    public class OoAccessibleDocAccessibleEventArgs : EventArgs
    {
        public readonly AccessibleEventObject E;

        public OoAccessibleDocAccessibleEventArgs(AccessibleEventObject aEvent)
        {
            E = aEvent;
        }
    }

    #endregion

    #region Enums

    public enum WindowEventType
    {
        none,
        disabled,
        enabled,
        hidden,
        moved,
        rezized,
        shown,
        disposing,
        unkown
    }

    #endregion
}