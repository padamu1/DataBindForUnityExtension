using Slash.Unity.DataBind.Core.Presentation;
using Slash.Unity.DataBind.Core.Utils;
using Slash.Unity.DataBind.Foundation.Setters;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimulFactory.DataBindForUnityExtension.UI.Setter
{
    public class CircularItemsSetter : ItemsSetter, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private class Item
        {
            public object Context { get; set; }

            public GameObject GameObject { get; set; }
        }

        public bool horizontal;

        [Header("pivot must be (0.5, 0.5)")]
        public RectTransform contentRect;

        [Header("minimun value is 5")]
        public int showCount = 5;

        public float loadPositionOffset;

        public float indexLoadPos;

        public bool isHighlightCenterItem;
        public float highlightScale = 1.25f;

        public float autoMoveToTargetSpeed = 30f;

        public float moveToCenterSpeed = 20f;

        private int mainIndex = 0;
        private bool isWaiting;

        private void OnValidate()
        {
            if (showCount < 5)
            {
                showCount = 5;
            }
        }

        /// <summary>
        ///     Items.
        /// </summary>
        private readonly List<Item> items = new List<Item>();

        /// <summary>
        ///     Prefab to create the items from.
        /// </summary>
        public GameObject Prefab;

        private CanvasScaler canvasScaler;
        private float scrollPower;

        /// <summary>
        ///     Returns an enumerator for the contexts of all items.
        /// </summary>
        protected IEnumerable<object> ItemContexts
        {
            get { return this.items.Select(item => item.Context); }
        }

        /// <summary>
        ///     Returns an enumerator for the game objects of all items.
        /// </summary>
        protected IEnumerable<GameObject> ItemGameObjects
        {
            get { return this.items.Select(item => item.GameObject); }
        }

        private List<object> contexts = new List<object>();
        /// <inheritdoc />
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

            if (this.Prefab != null)
            {
                this.Prefab.SetActive(true);
            }

            base.Init();
        }

        private void DestroyAll()
        {
            foreach (var item in this.items)
            {
                this.OnItemDestroyed(item.Context, item.GameObject);
                Destroy(item.GameObject);
            }

            mainIndex = 0;

            this.items.Clear();
        }

        /// <summary>
        ///     Clears all created items.
        /// </summary>
        protected override void ClearItems()
        {
            DestroyAll();

            contexts.Clear();
        }

        private bool LoadIndexUp()
        {
            if (contexts.Count < showCount)
            {
                return false;
            }

            int halfOfShowCount = showCount / 2;

            int loadIndex = mainIndex + 1 + halfOfShowCount;

            if (loadIndex >= contexts.Count)
            {
                loadIndex -= contexts.Count;
            }

            LoadItem(loadIndex, false);

            int unloadIndex = mainIndex - halfOfShowCount;

            if (unloadIndex < 0)
            {
                unloadIndex = contexts.Count + unloadIndex;
            }

            UnLoadItem(unloadIndex);

            mainIndex = (mainIndex + 1) % contexts.Count;

            return true;
        }

        private bool LoadIndexDown()
        {
            if (contexts.Count < showCount)
            {
                return false;
            }

            int halfOfShowCount = showCount / 2;

            int loadIndex = mainIndex - halfOfShowCount - 1; // 2 -> 0 - 1 = -1

            if (loadIndex < 0)
            {
                loadIndex = contexts.Count + loadIndex;
            }

            LoadItem(loadIndex, true);

            // 아래 부분 언로드
            int unloadIndex = mainIndex + halfOfShowCount;

            if (unloadIndex >= contexts.Count)
            {
                unloadIndex -= contexts.Count;
            }

            UnLoadItem(unloadIndex);

            mainIndex = (mainIndex - 1 + contexts.Count) % contexts.Count;

            return true;
        }

        private void UnLoadItem(int index)
        {
            RemovePageingItem(contexts[index]);
        }

        private void LoadItem(int loadIndex, bool isFront)
        {
            CreateItemWithLoad(contexts[loadIndex], loadIndex, isFront);
        }

        private void CreateItemWithLoad(object itemContext, int itemIndex, bool isFront)
        {
            // Instantiate item game object inactive to avoid duplicate initialization.
            this.Prefab.SetActive(false);
            var item = Instantiate(this.Prefab);

            this.items.Add(new Item { GameObject = item, Context = itemContext });
            item.transform.SetParent(this.Target, false);

            if (isFront)
            {
                item.transform.SetAsFirstSibling();
            }
            else
            {
                item.transform.SetAsLastSibling();
            }

            // Set item context after setup as the parent may change which influences the context path.
            if (itemContext != null)
            {
                // Set item data context.
                var itemContextHolder = item.GetComponent<ContextHolder>();
                if (itemContextHolder == null)
                {
                    itemContextHolder = item.AddComponent<ContextHolder>();
                }

                var path = this.Data.Type == DataBindingType.Context ? this.Data.Path : string.Empty;
                itemContextHolder.SetContext(itemContext, path + DataBindSettings.PathSeparator + itemIndex);
            }

            // Activate after the context was set.
            item.SetActive(true);
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

        /// <summary>
        ///     Called when an item for an item context was destroyed.
        /// </summary>
        /// <param name="itemContext">Item context the item was for.</param>
        /// <param name="itemObject">Item game object.</param>
        protected virtual void OnItemDestroyed(object itemContext, GameObject itemObject)
        {
        }

        private bool RemovePageingItem(object itemContext)
        {
            // Get item.
            var item = this.items.FirstOrDefault(existingItem => existingItem.Context == itemContext);
            if (item == null)
            {
                return false;
            }

            // Remove item.
            this.items.Remove(item);
            this.OnItemDestroyed(item.Context, item.GameObject);


            // Destroy item.
            Destroy(item.GameObject);
            return true;
        }

        /// <summary>
        ///     Removes the item with the specified item context.
        /// </summary>
        /// <param name="itemContext">Item context of the item to remove.</param>
        protected override void RemoveItem(object itemContext)
        {
            int itemIndex = contexts.IndexOf(itemContext);

            contexts.Remove(itemContext);

            if (!RemovePageingItem(itemContext))
            {
                return;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isWaiting)
            {
                return;
            }

            CheckPosition(eventData.delta);
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

            CheckPosition(eventData.delta);

            StartCoroutine(MoveToCenter());
        }

        private void CheckPosition(Vector2 delta)
        {
            if (horizontal)
            {
                contentRect.anchoredPosition += new Vector2(delta.x * scrollPower, 0);

                if (contentRect.anchoredPosition.x > indexLoadPos)
                {
                    if (LoadIndexDown())
                    {
                        contentRect.anchoredPosition = contentRect.anchoredPosition - new Vector2(loadPositionOffset, 0);
                    }
                }
                else if (contentRect.anchoredPosition.x < -1 * indexLoadPos)
                {
                    if (LoadIndexUp())
                    {
                        contentRect.anchoredPosition = contentRect.anchoredPosition + new Vector2(loadPositionOffset, 0);
                    }
                }
            }
            else
            {
                contentRect.anchoredPosition += new Vector2(0, delta.y * scrollPower);

                if (contentRect.anchoredPosition.y < -1 * indexLoadPos)
                {
                    if (LoadIndexDown())
                    {
                        contentRect.anchoredPosition = contentRect.anchoredPosition + new Vector2(0, loadPositionOffset);
                    }
                }
                else if (contentRect.anchoredPosition.y > indexLoadPos)
                {
                    if (LoadIndexUp())
                    {
                        contentRect.anchoredPosition = contentRect.anchoredPosition - new Vector2(0, loadPositionOffset);
                    }
                }
            }

            CheckHighlight();
        }

        private void CheckHighlight()
        {
            if (isHighlightCenterItem == false)
            {
                return;
            }
            
            if (horizontal)
            {
                #region main index item setting

                float t = Mathf.Abs(contentRect.anchoredPosition.x / indexLoadPos);
                t = Mathf.Clamp01(t); 

                var mainItem = this.items.FirstOrDefault(existingItem => existingItem.Context == contexts[mainIndex]);
                if (mainItem != null)
                {
                    mainItem.GameObject.transform.localScale = Vector3.one * Mathf.Lerp(highlightScale, 1f, t);
                }

                int leftIndex = (mainIndex - 1 + contexts.Count) % contexts.Count;
                int rightIndex = (mainIndex + 1) % contexts.Count;

                var leftItem = this.items.FirstOrDefault(existingItem => existingItem.Context == contexts[leftIndex]);
                var rightItem = this.items.FirstOrDefault(existingItem => existingItem.Context == contexts[rightIndex]);

                float scaleFactor = Mathf.Lerp(1f, highlightScale, t); 

                if (leftItem != null)
                {
                    leftItem.GameObject.transform.localScale = Vector3.one * (contentRect.anchoredPosition.x > 0 ? scaleFactor : 1f);
                }

                if (rightItem != null)
                {
                    rightItem.GameObject.transform.localScale = Vector3.one * (contentRect.anchoredPosition.x < 0 ? scaleFactor : 1f);
                }
                #endregion
            }
        }

        private IEnumerator MoveToCenter()
        {
            if (horizontal)
            {
                while (Mathf.Abs(contentRect.anchoredPosition.x - 0) > 0.01f * moveToCenterSpeed)
                {
                    contentRect.anchoredPosition = new Vector2(Mathf.Lerp(contentRect.anchoredPosition.x, 0, moveToCenterSpeed * Time.deltaTime), contentRect.anchoredPosition.y);

                    CheckHighlight();
                    yield return null;
                }
            }
            else
            {
                while (Mathf.Abs(contentRect.anchoredPosition.y - 0) > 0.01f * moveToCenterSpeed)
                {
                    contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, Mathf.Lerp(contentRect.anchoredPosition.y, 0, moveToCenterSpeed * Time.deltaTime));

                    CheckHighlight();
                    yield return null;
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
            base.Enable();

            OnAddItem();
        }

        public async Awaitable OnAddItem()
        {
            if (isWaiting)
            {
                return;
            }

            isWaiting = true;

            DestroyAll();

            mainIndex = 0;

            await Awaitable.NextFrameAsync();

            isWaiting = false;

            if (contexts == null || contexts.Count <= 0 || this.gameObject.activeSelf == false)
            {
                return;
            }

            for (int i = mainIndex - showCount / 2; i <= mainIndex + showCount / 2; i++)
            {
                int index = 0;

                if (i < 0)
                {
                    index = contexts.Count + i;
                }
                else
                {
                    index = i;
                }

                if (index < 0 || index >= contexts.Count)
                {
                    continue;
                }

                LoadItem(index, false);
            }

            var item = this.items.FirstOrDefault(existingItem => existingItem.Context == contexts[mainIndex]);
            item.GameObject.transform.localScale = Vector3.one * highlightScale;

            if (horizontal)
            {
                contentRect.anchoredPosition = new Vector2(0, contentRect.anchoredPosition.y);
            }
            else
            {
                contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0);
            }
        }

        public void MoveIndexUp()
        {
            StopAllCoroutines();
            StartCoroutine(MoveToDirection(new Vector3(-1, 1, 0) * autoMoveToTargetSpeed));
        }

        public void MoveIndexDown()
        {
            StopAllCoroutines();
            StartCoroutine(MoveToDirection(new Vector3(1, -1, 0) * autoMoveToTargetSpeed));
        }

        private IEnumerator MoveToDirection(Vector3 dir)
        {
            var currentMainIndex = mainIndex;

            while (currentMainIndex == mainIndex)
            {
                CheckPosition(dir);
                yield return null;
            }

            StartCoroutine(MoveToCenter());
        }

    }
}
