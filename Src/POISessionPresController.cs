using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using POILibCommunication;

namespace POIProxy
{
    
    public class POISessionPresController
    {
        int curSlideIndex = 0;
        int curDurationIndex = 0;
        int curSlidePlayOffset = -1;

        const int preloadOffset = 2;
        bool preloadStarted = false;

        int curPreloadIndex = -1;

        POIPresentation curPresentation;
        POISession mySession;

        public POIPresentation CurPres { get { return curPresentation; } }
        public int CurSlideIndex { get { return curSlideIndex; } }

        public POISessionPresController(POISession session)
        {
            mySession = session;
        }

        public POIPresentation getPresMsgTemplate()
        {
            return new POIPresentation(CurPres.PresID);
        }

        public void LoadPresentation(POIPresentation presentation)
        {
            curPresentation = presentation;
            Start();
        }

        public POISlide GetPreloadSlide()
        {
            int index = curSlideIndex + preloadOffset;
            
            if (index < curPresentation.Count && index > curPreloadIndex)
            {
                curPreloadIndex = index;
                return curPresentation.SlideAtIndex(index);
            }
            else
            {
                return null;
            }
        }

        public List<POISlide> GetInitialSlides()
        {
            List<POISlide> myList = new List<POISlide>();

            if (!preloadStarted)
            {
                curPreloadIndex = preloadOffset;
                preloadStarted = true;
            }
            
            for (int i = 0; i <= curPreloadIndex && i < curPresentation.Count; i++)
            {
                myList.Add(curPresentation.SlideAtIndex(i));
                //myList.Add(new POISlide(null));
            }

            return myList;
        }

        public void Start()
        {
            //Load the first slide
            curSlideIndex = 0;
            curDurationIndex = 0;

            //LoadSlide(curSlideIndex);
            LoadSlide(curPresentation.SlideAtIndex(curSlideIndex));
        }

        public void playNext()
        {
            if (curSlideIndex < curPresentation.Count)
            {
                //POISlide curSlide = myPresentation[curSlideIndex];
                POISlide curSlide = curPresentation.SlideAtIndex(curSlideIndex);
                //int curDuration = curSlide.GetPlayDurationNext();
                int curDuration = curSlide.GetDurationAtIndex(curDurationIndex);
                curDurationIndex++;

                POIGlobalVar.POIDebugLog(curDuration);

                if (curDuration < 0)
                {
                    if (curSlideIndex + 1 < curPresentation.Count)
                    {
                        curSlideIndex++;
                        LoadSlide(curPresentation.SlideAtIndex(curSlideIndex));

                        mySession.BroadcastPreloadSlide();
                    }
                    else
                    {
                        //Undo the increment of current duration list
                        curDurationIndex--;
                    }
                }
                else
                {
                    playAnimation(curSlidePlayOffset, curDuration);
                    curSlidePlayOffset += curDuration;
                }
            }
        }

        public void playPrev()
        {
            if (curSlideIndex > 0)
            {
                POISlide curSlide = curPresentation.SlideAtIndex(curSlideIndex);
                //int curDuration = curSlide.GetPlayDurationPrev();
                curDurationIndex--;
                int curDuration = curSlide.GetDurationAtIndex(curDurationIndex);

                POIGlobalVar.POIDebugLog(curDuration);

                if (curDuration < 0)
                {
                    //LoadSlide(curSlideIndex + 1);
                    curSlideIndex--;
                    LoadSlide(curPresentation.SlideAtIndex(curSlideIndex));

                }
                else
                {
                    //To do: seek to the previous starting point
                    curSlidePlayOffset -= curDuration;
                    JumpToPosition(curSlidePlayOffset);
                }
            }
        }

        public void JumpToSlide(int slideIndex)
        {
            curSlideIndex = slideIndex;
            LoadSlide(curPresentation.SlideAtIndex(curSlideIndex));
        }

        private void playAnimation(int startMS, int durationMS)
        {
            
        }

        private void JumpToPosition(int position)
        {
           
        }

        public void LoadSlide(POISlide curSlide)
        {
            curSlidePlayOffset = 0;
            curDurationIndex = 0;
        }
    }
}
