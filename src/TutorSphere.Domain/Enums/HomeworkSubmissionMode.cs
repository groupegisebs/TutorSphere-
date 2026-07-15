namespace TutorSphere.Domain.Enums;

[Flags]
public enum HomeworkSubmissionMode
{
    None = 0,
    Online = 1,
    PaperScan = 2,
    Video = 4,
    FileUpload = 8
}
