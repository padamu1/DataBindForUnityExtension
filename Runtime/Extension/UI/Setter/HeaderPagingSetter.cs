using Slash.Unity.DataBind.Core.Presentation;
using Slash.Unity.DataBind.Core.Utils;
using Slash.Unity.DataBind.Foundation.Setters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimulFactory.DataBindForUnityExtension.UI.Setter
{
    public interface IPagingHeader
    {

    }

    public interface IPagingObject
    {

    }

    /// <summary>
    /// Header가 있는 페이징 세터
    /// </summary>
    public abstract class HeaderPagingSetter : ItemsSetter, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        #region Header Pooling
        private Queue<(ContextHolder, RectTransform)> headerPool = new Queue<(ContextHolder, RectTransform)>();

        private void ReturnHeaderToPool((ContextHolder, RectTransform) obj)
        {
            obj.Item1.gameObject.SetActive(false);
            obj.Item1.Context = null;
            headerPool.Enqueue(obj);
        }

        private (ContextHolder, RectTransform) GetHeaderFromPool()
        {
            if (headerPool.Count > 0)
            {
                var item = headerPool.Dequeue();
                return item;
            }

            // Instantiate item game object inactive to avoid duplicate initialization.
            this.HeaderPrefab.SetActive(false);

            GameObject o = Instantiate(HeaderPrefab, this.Target, false);

            var rect = o.GetComponent<RectTransform>();
            rect.sizeDelta = headerSize;

            // Set item data context.
            var itemContextHolder = o.GetComponent<ContextHolder>();
            if (itemContextHolder == null)
            {
                itemContextHolder = o.AddComponent<ContextHolder>();
            }

            return (itemContextHolder, rect);
        }
        #endregion

        #region Object Pooling
        private Queue<(ContextHolder, RectTransform)> objectPool = new Queue<(ContextHolder, RectTransform)>();

        private void ReturnObjectToPool((ContextHolder, RectTransform) obj)
        {
            obj.Item1.gameObject.SetActive(false);
            obj.Item1.Context = null;
            objectPool.Enqueue(obj);
        }

        private (ContextHolder, RectTransform) GetObjectFromPool()
        {
            if (objectPool.Count > 0)
            {
                var item = objectPool.Dequeue();
                return item;
            }

            // Instantiate item game object inactive to avoid duplicate initialization.
            this.ObjectPrefab.SetActive(false);

            GameObject o = Instantiate(ObjectPrefab, this.Target, false);

            var rect = o.GetComponent<RectTransform>();
            rect.sizeDelta = objectSize;

            // Set item data context.
            var itemContextHolder = o.GetComponent<ContextHolder>();
            if (itemContextHolder == null)
            {
                itemContextHolder = o.AddComponent<ContextHolder>();
            }

            return (itemContextHolder, rect);
        }
        #endregion

        protected class Item
        {
            public object Context { get; set; }
            public RectTransform RectTransform { get; set; }
            public ContextHolder Holder { get; set; }
            public bool isHeader { get; set; }
        }

        public float positionSmoothSpeed;

        public bool horizontal;

        public RectTransform viewport;

        public RectTransform contentRect;

        public float loadSizeOffset;

        public Vector2 headerSize;

        public Vector2 objectSize;

        public int headerLoadCount = 1;
        public int objectLoadCount = 7;

        public float lineSpace;

        protected int firstIndex = 0;

        protected int lastIndex = 0;

        private bool isWaiting;

        protected readonly List<Item> items = new List<Item>();

        public Vector2 prefabAnchorMin;
        public Vector2 prefabAnchorMax;
        public Vector2 prefabPivot;

        public GameObject HeaderPrefab;
        public GameObject ObjectPrefab;

        protected List<object> contexts = new List<object>();

        private CancellationTokenSource cancellationTokenSource;

        private CanvasScaler canvasScaler;

        private float scrollPower = 1f;

        private void DestroyAll()
        {
            StopAllCoroutines();

            if (isWaiting == false && cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested == false)
            {
                cancellationTokenSource.Cancel();
            }

            try
            {
                foreach (var item in this.items)
                {
                    if (item.isHeader)
                    {
                        ReturnHeaderToPool((item.Holder, item.RectTransform));
                    }
                    else
                    {
                        ReturnObjectToPool((item.Holder, item.RectTransform));
                    }
                }

                this.items.Clear();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public override void Init()
        {
            canvasScaler = GetComponentInParent<CanvasScaler>();

            if (canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scrollPower = horizontal ? canvasScaler.referenceResolution.x / Screen.width : canvasScaler.referenceResolution.y / Screen.height;
            }
            else
            {
                scrollPower = 1;
            }

            var headerRect = HeaderPrefab.GetComponent<RectTransform>();
            headerRect.anchorMax = prefabAnchorMax;
            headerRect.anchorMin = prefabAnchorMin;
            headerRect.pivot = prefabAnchorMin;

            var objectRect = ObjectPrefab.GetComponent<RectTransform>();
            objectRect.anchorMax = prefabAnchorMax;
            objectRect.anchorMin = prefabAnchorMin;
            objectRect.pivot = prefabAnchorMin;

            base.Init();
        }

        /// <summary>
        ///     Clears all created items.
        /// </summary>
        protected override void ClearItems()
        {
            DestroyAll();

            contexts.Clear();
        }

        protected abstract void LoadIndexUp(int loadIndex);

        protected abstract void LoadIndexDown(int loadIndex);

        protected void UnLoadItem(int unloadIndex, bool isFront)
        {
            if (isFront)
            {
                firstIndex = unloadIndex + 1;
            }
            else
            {
                lastIndex = unloadIndex - 1;
            }

            RemovePageingItem(contexts[unloadIndex]);
        }

        protected void LoadItem(int loadIndex, bool isHeader, bool isFront, out Item item)
        {
            if (isFront)
            {
                firstIndex = loadIndex;
            }
            else
            {
                lastIndex = loadIndex;
            }

            CreateItemWithLoad(contexts[loadIndex], isHeader, loadIndex, isFront, out item);
        }

        private void CreateItemWithLoad(object itemContext, bool isHeader, int itemIndex, bool isFront, out Item item)
        {
            (ContextHolder, RectTransform) queueItem;
            if (isHeader)
            {
                queueItem = GetHeaderFromPool();
            }
            else
            {
                queueItem = GetObjectFromPool();
            }

            item = new Item { RectTransform = queueItem.Item2, Context = itemContext, Holder = queueItem.Item1, isHeader = isHeader };

            if (isFront)
            {
                this.items.Insert(0, item);
            }
            else
            {
                this.items.Add(item);
            }

            if (isFront)
            {
                queueItem.Item1.transform.SetAsFirstSibling();
            }
            else
            {
                queueItem.Item1.transform.SetAsLastSibling();
            }

            var path = this.Data.Type == DataBindingType.Context ? this.Data.Path : string.Empty;
            queueItem.Item1.SetContext(itemContext, path + DataBindSettings.PathSeparator + itemIndex);

            // Activate after the context was set.
            queueItem.Item1.gameObject.SetActive(true);
        }
        /// <summary>
        ///     Creates an item for the specified item context.
        /// </summary>
        /// <param name="itemContext">Item context for the item to create.</param>
        /// <param name="itemIndex">Index of item to create.</param>
        protected override void CreateItem(object itemContext, int itemIndex)
        {
            contexts.Insert(itemIndex, itemContext);

            OnAddItem();
        }

        private bool RemovePageingItem(object itemContext)
        {
            // Find item.
            var item = this.items.FirstOrDefault(existingItem => existingItem.Context == itemContext);
            if (item == null)
            {
                return false;
            }

            this.items.Remove(item);

            if (item.isHeader)
            {
                ReturnHeaderToPool((item.Holder, item.RectTransform));
            }
            else
            {
                ReturnObjectToPool((item.Holder, item.RectTransform));
            }

            return true;
        }

        /// <summary>
        ///     Removes the item with the specified item context.
        /// </summary>
        /// <param name="itemContext">Item context of the item to remove.</param>
        protected override void RemoveItem(object itemContext)
        {
            DestroyAll();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isWaiting)
            {
                return;
            }

            CheckPosition(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            StopAllCoroutines();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isWaiting)
            {
                return;
            }

            StartCoroutine(PositionSmooth());
        }

        private IEnumerator PositionSmooth()
        {
            if (horizontal)
            {
                while (contentRect.anchoredPosition.x <= 0)
                {
                    contentRect.anchoredPosition = new Vector2(Mathf.Lerp(contentRect.anchoredPosition.x, 0, Time.deltaTime * positionSmoothSpeed), contentRect.anchoredPosition.y);
                    yield return null;
                }

                while (contentRect.anchoredPosition.x > contentRect.rect.width - viewport.rect.width)
                {
                    contentRect.anchoredPosition = new Vector2(Mathf.Lerp(contentRect.anchoredPosition.x, contentRect.rect.width - viewport.rect.width, Time.deltaTime * positionSmoothSpeed), contentRect.anchoredPosition.y);
                    yield return null;
                }
            }
            else
            {
                while (contentRect.anchoredPosition.y <= 0)
                {
                    contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, Mathf.Lerp(contentRect.anchoredPosition.y, 0, Time.deltaTime * positionSmoothSpeed));
                    yield return null;
                }

                while (contentRect.anchoredPosition.y > contentRect.rect.height - viewport.rect.height)
                {
                    contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, Mathf.Lerp(contentRect.anchoredPosition.y, contentRect.rect.height - viewport.rect.height, Time.deltaTime * positionSmoothSpeed));
                    yield return null;
                }
            }
        }

        private void CheckPosition(PointerEventData eventData)
        {
            float scroll = horizontal ? eventData.delta.x : eventData.delta.y;
            scroll *= scrollPower;

            if (horizontal)
            {
                if (contentRect.rect.width < viewport.rect.width)
                {
                    return;
                }

                contentRect.anchoredPosition += new Vector2(scroll, 0);
            }
            else
            {
                if (contentRect.rect.height < viewport.rect.height)
                {
                    return;
                }

                contentRect.anchoredPosition += new Vector2(0, scroll);
            }

            if (scroll < 0)
            {
                CheckIndexDown();
            }
            else
            {
                CheckIndexUp();
            }
        }

        protected void CheckIndexUp()
        {
            if (horizontal)
            {
                if (lastIndex + 1 >= contexts.Count)
                {
                    return;
                }

                if (contentRect.anchoredPosition.x + items[0].RectTransform.anchoredPosition.x > loadSizeOffset)
                {
                    int loadIndex = lastIndex + 1;
                    LoadIndexUp(loadIndex);
                }
            }
            else
            {
                if (lastIndex + 1 >= contexts.Count)
                {
                    return;
                }

                if (contentRect.anchoredPosition.y + items[0].RectTransform.anchoredPosition.y > loadSizeOffset)
                {
                    int loadIndex = lastIndex + 1;
                    LoadIndexUp(loadIndex);
                }
            }

        }

        protected void CheckIndexDown()
        {
            if (horizontal)
            {
                if (firstIndex <= 0)
                {
                    return;
                }

                if (contentRect.anchoredPosition.x + items[items.Count - 1].RectTransform.anchoredPosition.x + viewport.rect.width < -1 * loadSizeOffset)
                {
                    int loadIndex = firstIndex - 1;
                    LoadIndexDown(loadIndex);
                }
            }
            else
            {
                if (firstIndex <= 0)
                {
                    return;
                }

                if (contentRect.anchoredPosition.y + items[items.Count - 1].RectTransform.anchoredPosition.y + viewport.rect.height < -1 * loadSizeOffset)
                {
                    int loadIndex = firstIndex - 1;
                    LoadIndexDown(loadIndex);
                }
            }
        }

        public override void Disable()
        {
            DestroyAll();
            base.Disable();
        }

        public override void Enable()
        {
            DestroyAll();

            OnAddItem();

            base.Enable();
        }

        public async Awaitable OnAddItem()
        {
            if (isWaiting)
            {
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();

            isWaiting = true;

            await Awaitable.NextFrameAsync();

            isWaiting = false;

            if (cancellationTokenSource.IsCancellationRequested == false)
            {
                try
                {
                    await InitLoad(cancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        protected abstract Awaitable InitLoad(CancellationToken cancellationToken);

        protected bool CheckHeader(int index)
        {
            if (contexts[index] is IPagingHeader)
            {
                return true;
            }
            else if (contexts[index] is IPagingObject)
            {
                return false;
            }
            else
            {
                throw new FormatException();
            }
        }
    }
}
