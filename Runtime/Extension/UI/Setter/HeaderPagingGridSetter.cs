using System.Threading;
using UnityEngine;

namespace SimulFactory.DataBindForUnityExtension.UI.Setter
{
    public class HeaderPagingGridSetter : HeaderPagingSetter
    {
        public float onceLoadRate = 2;

        public float headerPadding;
        public float objectPadding;

        public float objectGridSpace;

        public float maxLoadIndex;

        public bool reverse;

        protected override async Awaitable InitLoad(CancellationToken cancellationToken)
        {
            firstIndex = 0;
            lastIndex = 0;

            if (horizontal)
            {
                contentRect.sizeDelta = new Vector2(0, contentRect.sizeDelta.y);
            }
            else
            {
                contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 0);
            }

            if (horizontal)
            {
                contentRect.anchoredPosition = new Vector2(0, contentRect.anchoredPosition.y);
            }
            else
            {
                contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0);
            }

            lastIndex = -1;
            maxLoadIndex = -1;

            contentRect.sizeDelta = horizontal ? new Vector2(0, contentRect.sizeDelta.y) : new Vector2(contentRect.sizeDelta.x, 0);

            while ((horizontal && viewport.rect.width + loadSizeOffset > contentRect.sizeDelta.x) || (!horizontal && viewport.rect.height + loadSizeOffset > contentRect.sizeDelta.y))
            {
                if (contexts.Count <= lastIndex + 1)
                {
                    return;
                }

                AddItemIndexUp(lastIndex + 1);

                await Awaitable.NextFrameAsync();

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        protected override void LoadIndexUp(int loadIndex)
        {
            for (int i = 0; i < onceLoadRate; i++)
            {
                if (i == 0)
                {
                    AddItemIndexUp(loadIndex);
                }
                else
                {
                    if (lastIndex + 1 >= contexts.Count)
                    {
                        break;
                    }
                    AddItemIndexUp(lastIndex + 1);
                }

                UnloadItemIndexUp();
            }

            CheckIndexUp();
        }

        private void AddItemIndexUp(int loadIndex)
        {
            float lastItemPos;
            float lastItemSize;
            if (items.Count == 0)
            {
                lastItemPos = 0;
                lastItemSize = 0;
            }
            else
            {
                lastItemPos = horizontal ? items[items.Count - 1].RectTransform.anchoredPosition.x : items[items.Count - 1].RectTransform.anchoredPosition.y;
                lastItemSize = horizontal ? items[items.Count - 1].RectTransform.sizeDelta.x : items[items.Count - 1].RectTransform.sizeDelta.y;
            }


            bool isHeader = CheckHeader(loadIndex);

            if (isHeader == false)
            {
                for (int i = loadIndex; i < loadIndex + objectLoadCount; i++)
                {
                    if (contexts.Count <= i || CheckHeader(i) == true)
                    {
                        break;
                    }

                    LoadItem(i, false, false, out Item item);

                    SetObjectItemPosition(item.RectTransform, i - loadIndex, lastItemPos, lastItemSize, false, reverse);
                }

                CheckMaxIndex(objectSize);
            }
            else
            {
                LoadItem(loadIndex, true, false, out Item item);

                SetHeaderItemPosition(item.RectTransform, lastItemPos, lastItemSize, false);

                CheckMaxIndex(headerSize);
            }
        }

        private void UnloadItemIndexUp()
        {
            bool isFrontHeader = items[0].isHeader;
            if (isFrontHeader)
            {
                UnLoadItem(firstIndex, true);
            }
            else
            {
                float frontItemPos = horizontal ? items[0].RectTransform.anchoredPosition.x : items[0].RectTransform.anchoredPosition.y;

                while ((horizontal && items[0].RectTransform.anchoredPosition.x == frontItemPos) || (!horizontal && items[0].RectTransform.anchoredPosition.y == frontItemPos))
                {
                    UnLoadItem(firstIndex, true);
                }
            }
        }

        private void CheckMaxIndex(Vector2 value)
        {
            if (maxLoadIndex < lastIndex)
            {
                maxLoadIndex = lastIndex;

                if (horizontal)
                {
                    contentRect.sizeDelta += new Vector2(lineSpace + value.x, 0);
                }
                else
                {
                    contentRect.sizeDelta += new Vector2(0, lineSpace + value.y);
                }
            }
        }

        protected override void LoadIndexDown(int loadIndex)
        {
            for (int i = 0; i < onceLoadRate; i++)
            {
                if (i == 0)
                {
                    AddItemIndexDown(loadIndex);
                }
                else
                {
                    if (firstIndex - 1 < 0)
                    {
                        break;
                    }

                    AddItemIndexDown(firstIndex - 1);
                }
                UnloadItemIndexDown();
            }

            CheckIndexDown();
        }

        private void AddItemIndexDown(int loadIndex)
        {
            float firstItemPos = horizontal ? items[0].RectTransform.anchoredPosition.x : items[0].RectTransform.anchoredPosition.y;
            float firstItemSize = horizontal ? items[0].RectTransform.sizeDelta.x : items[0].RectTransform.sizeDelta.y;

            bool isHeader = CheckHeader(loadIndex);

            if (isHeader == false)
            {

                int nextHeaderIndex = loadIndex - 1;
                while (nextHeaderIndex >= 0)
                {
                    if (CheckHeader(nextHeaderIndex))
                    {
                        break;
                    }

                    --nextHeaderIndex;
                }

                int loadCount = (loadIndex - nextHeaderIndex) % objectLoadCount;
                if (loadCount == 0)
                {
                    loadCount = objectLoadCount;
                }

                for (int i = loadIndex; i > loadIndex - loadCount; i--)
                {
                    if (i >= 0 && CheckHeader(i) == true)
                    {
                        break;
                    }

                    LoadItem(i, false, true, out Item item);

                    SetObjectItemPosition(item.RectTransform, -1 * (loadIndex - loadCount - i) - 1, firstItemPos, firstItemSize, true, reverse);
                }
            }
            else
            {
                LoadItem(loadIndex, true, true, out Item item);

                SetHeaderItemPosition(item.RectTransform, firstItemPos, firstItemSize, true);
            }
        }

        private void UnloadItemIndexDown()
        {
            bool isLastHeader = items[items.Count - 1].isHeader;
            if (isLastHeader)
            {
                UnLoadItem(lastIndex, false);
            }
            else
            {
                float lastItemPos = horizontal ? items[items.Count - 1].RectTransform.anchoredPosition.x : items[items.Count - 1].RectTransform.anchoredPosition.y;

                while ((horizontal && items[items.Count - 1].RectTransform.anchoredPosition.x == lastItemPos) || (!horizontal && items[items.Count - 1].RectTransform.anchoredPosition.y == lastItemPos))
                {
                    UnLoadItem(lastIndex, false);
                }
            }
        }

        private void SetHeaderItemPosition(RectTransform rect, float lastLinePosition, float lastItemSize, bool addFront)
        {
            if (horizontal)
            {
                float x = addFront ? lastLinePosition - lineSpace - headerSize.x : lastLinePosition + lastItemSize + lineSpace;
                float y = headerPadding;

                rect.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                float x = headerPadding;

                float y = addFront ? lastLinePosition + lineSpace + headerSize.y : lastLinePosition - lastItemSize - lineSpace;

                rect.anchoredPosition = new Vector2(x, y);
            }
        }

        private void SetObjectItemPosition(RectTransform rect, int gridIndex, float lastLinePosition, float lastItemSize, bool addFront, bool reverse)
        {
            if (horizontal)
            {
                float x = addFront ? lastLinePosition - lineSpace - objectSize.x : lastLinePosition + lastItemSize + lineSpace;
                float y;
                if (reverse)
                {
                    y = objectPadding + (objectSize.y + objectGridSpace) * (objectLoadCount - 1 - gridIndex);
                }
                else
                {
                    y = objectPadding + (objectSize.y + objectGridSpace) * gridIndex;
                }

                rect.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                float x;
                if (reverse)
                {
                    x = objectPadding + (objectSize.x + objectGridSpace) * (objectLoadCount - 1 - gridIndex);
                }
                else
                {
                    x = objectPadding + (objectSize.x + objectGridSpace) * gridIndex;
                }

                float y = addFront ? lastLinePosition + lineSpace + objectSize.y : lastLinePosition - lastItemSize - lineSpace;

                rect.anchoredPosition = new Vector2(x, y);
            }
        }
    }
}
