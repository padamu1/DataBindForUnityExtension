// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GameObjectItemsSetter.cs" company="Slash Games">
//   Copyright (c) Slash Games. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SimulFactory.DataBindForUnityExtension.UI.Setter
{
    using System.Collections.Generic;
    using System.Linq;
    using Slash.Unity.DataBind.Core.Presentation;
    using Slash.Unity.DataBind.Core.Utils;
    using Slash.Unity.DataBind.Foundation.Setters;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;

#endif

    /// <summary>
    ///     Base class which adds game objects for each item of an ItemsSetter.
    /// </summary>
    [AddComponentMenu("Data Bind/Foundation/Setters/[DB] Game Object Items Paging Setter")]
    public class GameObjectItemsPagingSetter : ItemsSetter
    {
        private class Item
        {
            public object Context { get; set; }

            public GameObject GameObject { get; set; }
        }

        [Header("Content 달아야함")]
        public RectTransform scrollRect;
        private Vector2 baseRectAnchorPos;
        public int ShowCount;
        public int LoadCount = 5;
        public float LoadDownPos = 30;
        public float LoadUpPos = 10;
        public float AfterLoadDownPos = 10;
        public float AfterLoadUpPos = 10;
        private int CurrentStartIndex;
        private int CurrentEndIndex;

        private bool scrolling;

        /// <summary>
        ///     Items.
        /// </summary>
        private readonly List<Item> items = new List<Item>();

        /// <summary>
        ///     Prefab to create the items from.
        /// </summary>
        public GameObject Prefab;

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
            baseRectAnchorPos = scrollRect.anchoredPosition;
            if (this.Prefab != null)
            {
                this.Prefab.SetActive(true);
            }

            base.Init();
        }

        protected override void OnBindingValuesChanged()
        {
            contexts.Clear();
            base.OnBindingValuesChanged();
        }

        private void DestroyAll()
        {
            foreach (var item in this.items)
            {
                this.OnItemDestroyed(item.Context, item.GameObject);
                Destroy(item.GameObject);
            }

            CurrentStartIndex = 0;
            CurrentEndIndex = 0;
            scrollRect.anchoredPosition = baseRectAnchorPos;

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
            if (CurrentStartIndex == 0)
            {
                return false;
            }

            int currentLoadCount = LoadUpWithIndex();

            int unloadCount = (CurrentEndIndex - CurrentStartIndex) - ShowCount;

            UnloadDownWithIndex(unloadCount);
            return true;
        }

        private int LoadUpWithIndex()
        {
            int currentLoadCount = 0;

            if (CurrentStartIndex - LoadCount > 0)
            {
                LoadItem(CurrentStartIndex, CurrentStartIndex - LoadCount);

                CurrentStartIndex = CurrentStartIndex - LoadCount;

                currentLoadCount = LoadCount;
            }
            else
            {
                LoadItem(CurrentStartIndex, 0);

                currentLoadCount = CurrentStartIndex;

                CurrentStartIndex = 0;
            }

            return currentLoadCount;
        }

        private void UnloadDownWithIndex(int unloadCount)
        {
            UnLoadItem(CurrentEndIndex - unloadCount, CurrentEndIndex);
            CurrentEndIndex = CurrentEndIndex - unloadCount;
        }

        private bool LoadIndexDown()
        {
            if (CurrentEndIndex >= contexts.Count)
            {
                // 스크롤 변화 없음
                return false;
            }

            int currentLoadCount = LoadDownWithIndex();

            if (CurrentEndIndex == contexts.Count && contexts.Count % ShowCount > 0)
            {
                return false;
            }

            UnloadUpWithIndex(currentLoadCount);

            return true;
        }

        private int LoadDownWithIndex()
        {
            int currentLoadCount = 0;

            // 로드 시작
            if (CurrentEndIndex + LoadCount >= contexts.Count)
            {
                LoadItem(CurrentEndIndex, contexts.Count);

                currentLoadCount = contexts.Count - CurrentEndIndex;

                CurrentEndIndex = contexts.Count;
            }
            else
            {
                LoadItem(CurrentEndIndex, CurrentEndIndex + LoadCount);

                CurrentEndIndex = CurrentEndIndex + LoadCount;

                currentLoadCount = LoadCount;
            }

            return currentLoadCount;
        }

        private void UnloadUpWithIndex(int unloadCount)
        {
            UnLoadItem(CurrentStartIndex, CurrentStartIndex + unloadCount);
            this.CurrentStartIndex = CurrentStartIndex + unloadCount;
        }
        private void UnLoadItem(int startIndex, int endIndex)
        {
            for (int count = startIndex; count < endIndex; count++)
            {
                RemovePageingItem(contexts[count]);
            }
        }

        private void LoadItem(int startIndex, int endIndex)
        {
            if (startIndex < endIndex)
            {
                for (int count = startIndex; count < endIndex; count++)
                {
                    CreateItemWithLoad(contexts[count], count, false, false);
                }
            }
            else
            {
                for (int count = startIndex - 1; count >= endIndex; count--)
                {
                    CreateItemWithLoad(contexts[count], count, false, true);
                }
            }
        }
        private void CreateItemWithLoad(object itemContext, int itemIndex, bool targetIndex = false, bool isInsertFront = true)
        {
            // Instantiate item game object inactive to avoid duplicate initialization.
            this.Prefab.SetActive(false);
            var item = Instantiate(this.Prefab);

            this.items.Add(new Item { GameObject = item, Context = itemContext });
            item.transform.SetParent(this.Target, false);

            if(targetIndex)
            {
                item.transform.SetSiblingIndex(itemIndex);
            }
            else
            {
                if (isInsertFront)
                {
                    item.transform.SetAsFirstSibling();
                }
                else
                {
                    item.transform.SetAsLastSibling();
                }
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

            CheckShowCount(itemIndex);
        }

        private void CheckShowCount(int index)
        {
            if(index < CurrentStartIndex)
            {
                return;
            }
            else if(CurrentEndIndex > contexts.Count - 1)
            {
                return;
            }

            int leftShowCount = ShowCount - (CurrentEndIndex - CurrentStartIndex);

            if (leftShowCount > 0 && contexts.Count > CurrentEndIndex)
            {
                if (index < CurrentEndIndex)
                {
                    CreateItemWithLoad(contexts[index], index, true);
                }
                else
                {
                    LoadItem(CurrentEndIndex, CurrentEndIndex + 1);
                }

                ++CurrentEndIndex;
            }
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

            if(itemIndex < CurrentStartIndex)
            {
                --CurrentStartIndex;
            }

            if(itemIndex < CurrentEndIndex)
            {
                --CurrentEndIndex;
            }

            if (!RemovePageingItem(itemContext))
            {
                return;
            }

            if (contexts.Count > CurrentEndIndex + 1)
            {
                LoadItem(CurrentEndIndex, CurrentEndIndex + 1);
                ++CurrentEndIndex;
            }
            else if (CurrentStartIndex > 0)
            {
                LoadItem(CurrentStartIndex, CurrentStartIndex - 1);
                --CurrentStartIndex;
            }
        }

        private void FixedUpdate()
        {
            bool stopScrolling = false;

            bool isTouching = Input.touchCount > 0;
            bool isClicking = Input.GetMouseButton(0);

            if ((isTouching || isClicking) && scrolling == false)
            {
                scrolling = true;
            }
            else if (!isTouching && !isClicking && scrolling == true)
            {
                scrolling = false;
                stopScrolling = true;
            }

            if (stopScrolling)
            {
                if (scrollRect.anchoredPosition.y > LoadDownPos)
                {
                    if (LoadIndexDown())
                    {
                        float diff = LoadDownPos - scrollRect.anchoredPosition.y;

                        float newPos = AfterLoadDownPos - diff;

                        while (newPos > LoadDownPos)
                        {
                            LoadIndexDown();

                            newPos = AfterLoadDownPos - (LoadDownPos - newPos);
                        }

                        scrollRect.anchoredPosition = new Vector2(scrollRect.anchoredPosition.x, newPos);
                    }
                }
                else if (scrollRect.anchoredPosition.y < LoadUpPos)
                {
                    if (LoadIndexUp())
                    {
                        float diff = scrollRect.anchoredPosition.y - LoadUpPos;

                        float newPos = AfterLoadUpPos + diff;

                        while (newPos < LoadUpPos)
                        {
                            LoadIndexDown();

                            newPos = AfterLoadUpPos + (newPos - LoadUpPos);
                        }

                        scrollRect.anchoredPosition = new Vector2(scrollRect.anchoredPosition.x, newPos);
                    }
                }
            }
        }

        public void OnValueChanged(Vector2 vector2)
        {
            if (scrolling)
            {
            }
            else
            {
                if (scrollRect.anchoredPosition.y > LoadDownPos)
                {
                    if (LoadIndexDown())
                    {
                        float diff = (LoadDownPos - scrollRect.anchoredPosition.y) / 2;

                        scrollRect.anchoredPosition = new Vector2(scrollRect.anchoredPosition.x, AfterLoadDownPos - diff);
                    }
                }
                else if (scrollRect.anchoredPosition.y < LoadUpPos)
                {
                    if (LoadIndexUp())
                    {
                        float diff = (scrollRect.anchoredPosition.y - LoadUpPos) / 2;

                        scrollRect.anchoredPosition = new Vector2(scrollRect.anchoredPosition.x, AfterLoadUpPos - diff);
                    }
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

            DestroyAll();
            CurrentStartIndex = 0;
            LoadItem(0, ShowCount > contexts.Count ? contexts.Count : ShowCount);
            CurrentEndIndex = ShowCount > contexts.Count ? contexts.Count : ShowCount;
        }
    }
}