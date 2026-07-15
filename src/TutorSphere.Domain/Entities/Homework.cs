using TutorSphere.Domain.Common;
using TutorSphere.Domain.Enums;

namespace TutorSphere.Domain.Entities;

public class Homework : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? LessonId { get; set; }

    /// <summary>Regroupe les copies créées ensemble pour plusieurs élèves.</summary>
    public Guid? AssignmentGroupId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTime? DueDate { get; set; }
    public int? EstimatedMinutes { get; set; }

    /// <summary>JSON : blocs de contenu (texte, vidéo, fichier, lien, formulaire).</summary>
    public string? ContentJson { get; set; }

    /// <summary>JSON : critères d'évaluation [{name, points}].</summary>
    public string? CriteriaJson { get; set; }

    public HomeworkSubmissionMode SubmissionModes { get; set; } = HomeworkSubmissionMode.Online;
    public bool IsDraft { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public string? SubmissionNotes { get; set; }
    public decimal? Grade { get; set; }
    public string? Feedback { get; set; }
    public bool IsGraded { get; set; }

    public Student Student { get; set; } = null!;
    public Lesson? Lesson { get; set; }
}
