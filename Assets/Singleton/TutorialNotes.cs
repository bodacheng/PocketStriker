using mainMenu;

// 由于读取静态物体的效率可能更高，因此战斗场景我们真的就是可能搞成好几个不同的scene。
// 但是，系统角度讲整个程序还是分成主菜单类和战斗场景类两个大的scene，那么这个地方我们针对这两个大的scene，
// 分别搞了个用于记录的单元。

public class TutorialNotes
{
    public bool A1ButtonPressed = false;

    private static TutorialNotes instance;
    public static TutorialNotes Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new TutorialNotes();
            }
            return instance;
        }
    }
}