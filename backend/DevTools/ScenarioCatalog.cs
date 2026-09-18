using CourseInquiryDashboard.Models;
using CourseInquiryDashboard.Models.Dtos;

namespace CourseInquiryDashboard.DevTools;

/// <summary>A named scenario and the rows it produces (built on demand).</summary>
public sealed record ScenarioDefinition(string Name, string Description, Func<IReadOnlyList<SeedInquiry>> Build);

/// <summary>
/// The fixed catalog of dev seed scenarios. <see cref="Demo"/> is a curated,
/// realistic dataset (all five statuses, two pages, a few null optional fields);
/// the other scenarios are derived from it or generated to target a specific UI
/// condition the dashboard should handle.
/// </summary>
/// <remarks>
/// Every <see cref="SeedInquiry.Message"/> here is written as a visitor's
/// first-contact inquiry — the text they submit while the row is still
/// <see cref="Status.New"/> — and stays true regardless of the status the row is
/// later moved to. It never narrates a later stage ("paid in full", "chose another
/// provider"), which would be a staff note, not intake the visitor could have typed.
/// The realistic spread of <c>CreatedDate</c>/<c>UpdatedDate</c> is applied by the
/// seeder, not encoded here.
/// </remarks>
public static class ScenarioCatalog
{
    /// <summary>
    /// The canonical Merrithew/STOTT course names. Both the curated <see cref="Demo"/>
    /// rows and <see cref="GenerateLoad"/> draw from this one list, so the two can
    /// never drift onto different course vocabularies.
    /// </summary>
    private static class Course
    {
        public const string IntensiveMatPlus = "Intensive Mat-Plus (IMP)";
        public const string IntensiveReformer = "Intensive Reformer (IR)";
        public const string IntensiveCadillacChairBarrels = "Intensive Cadillac, Chair & Barrels (ICCB)";
        public const string TotalBarreFoundation = "Total Barre Foundation Course";
        public const string ZengaMatReformer = "ZEN•GA Mat & Reformer";
        public const string HaloTrainerFundamentals = "Halo Trainer Fundamentals";
        public const string CoreAthleticConditioning = "CORE Athletic Conditioning";
        public const string InjuriesAndSpecialPopulations = "Injuries & Special Populations";
        public const string RehabReformer = "Rehab: Reformer";
        public const string PilatesForGolf = "Pilates for Golf";
        public const string FunctionalAnatomy = "Functional Anatomy for Movement & Injuries";
        public const string PrenatalPilates = "Prenatal Pilates";

        /// <summary>The full catalog, for scenarios that cycle through every course.</summary>
        public static readonly string[] All =
        [
            IntensiveMatPlus, IntensiveReformer, IntensiveCadillacChairBarrels, TotalBarreFoundation,
            ZengaMatReformer, HaloTrainerFundamentals, CoreAthleticConditioning, InjuriesAndSpecialPopulations,
            RehabReformer, PilatesForGolf, FunctionalAnatomy, PrenatalPilates,
        ];
    }

    // NOTE: Demo is declared before All/Infos so it is initialized first — Infos
    // evaluates the scenario builders eagerly and would otherwise read a null Demo.
    private static readonly IReadOnlyList<SeedInquiry> Demo =
    [
        new("Emma", "Thompson", "emma.thompson@gmail.com", Course.IntensiveMatPlus, Status.New,
            Phone: "+1-416-555-0142", PreferredLocation: "Toronto",
            Message: "Looking to start my certification this fall. Do weekend cohorts exist?"),
        new("Liam", "O'Brien", "liam.obrien@outlook.com", Course.IntensiveReformer, Status.New,
            PreferredLocation: "Vancouver",
            Message: "Is prior mat certification required before the reformer intensive?"),
        new("Sofia", "Moretti", "sofia.moretti@icloud.com", Course.TotalBarreFoundation, Status.Contacted,
            Phone: "+1-604-555-0198", PreferredLocation: "Vancouver"),
        new("Noah", "Kim", "noah.kim@gmail.com", Course.ZengaMatReformer, Status.New,
            Phone: "+1-514-555-0176", PreferredLocation: "Montreal",
            Message: "Interested in the mind-body programming for my studio."),
        new("Aisha", "Rahman", "aisha.rahman@yahoo.com", Course.InjuriesAndSpecialPopulations, Status.Registered,
            Phone: "+1-212-555-0110", PreferredLocation: "New York",
            Message: "I work with post-surgical clients and want to add this to my practice — when's the next New York cohort?"),
        new("Lucas", "Silva", "lucas.silva@gmail.com", Course.CoreAthleticConditioning, Status.Pending,
            Phone: "+55-11-95555-0133", PreferredLocation: "Los Angeles",
            Message: "Waiting on my employer to approve professional development funds."),
        new("Chloe", "Dubois", "chloe.dubois@gmail.com", Course.FunctionalAnatomy, Status.New),
        new("Ethan", "Walker", "ethan.walker@gmail.com", Course.IntensiveCadillacChairBarrels, Status.Closed,
            Phone: "+1-312-555-0164", PreferredLocation: "Chicago",
            Message: "What are the prerequisites for the Cadillac, Chair & Barrels intensive?"),
        new("Mia", "Nguyen", "mia.nguyen@gmail.com", Course.HaloTrainerFundamentals, Status.Contacted,
            Phone: "+1-416-555-0187", PreferredLocation: "Toronto",
            Message: "Following up after the info session."),
        new("Oliver", "Schmidt", "oliver.schmidt@web.de", Course.IntensiveReformer, Status.New,
            Phone: "+49-30-5555-0121", PreferredLocation: "Berlin",
            Message: "Do you offer the course materials in German?"),
        new("Isabella", "Rossi", "isabella.rossi@gmail.com", Course.TotalBarreFoundation, Status.Pending,
            Phone: "+39-06-5555-0155", PreferredLocation: "London",
            Message: "Comparing dates between the London and Toronto host sites."),
        new("James", "Anderson", "james.anderson@gmail.com", Course.IntensiveMatPlus, Status.Registered,
            Phone: "+61-2-5555-0190", PreferredLocation: "Sydney"),
        new("Amara", "Okafor", "amara.okafor@gmail.com", Course.PilatesForGolf, Status.New,
            Phone: "+1-647-555-0173", PreferredLocation: "Toronto",
            Message: "Coaching a golf team and want sport-specific programming."),
        new("Daniel", "Lee", "daniel.lee@gmail.com", Course.ZengaMatReformer, Status.Contacted,
            Phone: "+65-5555-0148", PreferredLocation: "Singapore",
            Message: "Could you send the full ZEN•GA syllabus? An advisor recommended it for my studio."),
        new("Grace", "Patel", "grace.patel@gmail.com", Course.RehabReformer, Status.Closed,
            PreferredLocation: "London",
            Message: "I'm a physiotherapist — are there upcoming reformer rehab dates in the UK?"),
        new("Benjamin", "Martin", "benjamin.martin@gmail.com", Course.CoreAthleticConditioning, Status.Registered,
            Phone: "+1-323-555-0129", PreferredLocation: "Los Angeles"),
        new("Yuki", "Tanaka", "yuki.tanaka@gmail.com", Course.IntensiveCadillacChairBarrels, Status.Pending,
            Phone: "+81-3-5555-0102", PreferredLocation: "Tokyo",
            Message: "Need confirmation the course is taught in English."),
        new("Charlotte", "Wilson", "charlotte.wilson@gmail.com", Course.PrenatalPilates, Status.Registered,
            Phone: "+1-416-555-0119", PreferredLocation: "Toronto",
            Message: "Expecting this spring and keen to train beforehand — is the prenatal course open to new instructors?"),
        new("Mateo", "Garcia", "mateo.garcia@gmail.com", Course.InjuriesAndSpecialPopulations, Status.New,
            Phone: "+34-91-5555-0166", PreferredLocation: "Miami",
            Message: "Physiotherapist looking to add Pilates rehab to my practice."),
        new("Hannah", "Cohen", "hannah.cohen@gmail.com", Course.FunctionalAnatomy, Status.Contacted,
            Phone: "+1-617-555-0184", PreferredLocation: "Boston"),
        new("Arjun", "Sharma", "arjun.sharma@gmail.com", Course.TotalBarreFoundation, Status.New,
            Phone: "+971-4-5555-0177", PreferredLocation: "Dubai",
            Message: "Opening a boutique studio and need certified instructors."),
        new("Freya", "Johansson", "freya.johansson@gmail.com", Course.HaloTrainerFundamentals, Status.Pending,
            Message: "Interested in the Halo Trainer course — can I reserve a place while I finalize my schedule?"),
        new("William", "Brown", "william.brown@gmail.com", Course.IntensiveReformer, Status.Registered,
            Phone: "+1-403-555-0138", PreferredLocation: "Calgary",
            Message: "Our studio wants to certify three instructors on reformer — do you offer group rates?"),
        new("Zoe", "Campbell", "zoe.campbell@gmail.com", Course.IntensiveMatPlus, Status.Closed,
            Phone: "+1-604-555-0151", PreferredLocation: "Vancouver",
            Message: "Comparing mat certification providers — what sets your Intensive Mat-Plus apart?"),
        new("Gabriel", "Santos", "gabriel.santos@gmail.com", Course.PilatesForGolf, Status.Pending,
            Phone: "+55-21-5555-0193", PreferredLocation: "Miami"),
        new("Olivia", "Taylor", "olivia.taylor@gmail.com", Course.ZengaMatReformer, Status.Contacted,
            Phone: "+44-20-5555-0107", PreferredLocation: "London",
            Message: "Please reach me by email — I'd like the next ZEN•GA course dates in London."),
        new("Henry", "Nakamura", "henry.nakamura@gmail.com", Course.CoreAthleticConditioning, Status.Closed,
            Phone: "+1-416-555-0160", PreferredLocation: "Toronto",
            Message: "Personal trainer wanting to expand my scope."),
        new("Layla", "Haddad", "layla.haddad@gmail.com", Course.InjuriesAndSpecialPopulations, Status.Contacted,
            Phone: "+1-514-555-0145", PreferredLocation: "Montreal",
            Message: "Do you offer payment plans for the special populations course?"),
    ];

    /// <summary>Scenarios keyed by name (case-insensitive).</summary>
    public static IReadOnlyDictionary<string, ScenarioDefinition> All { get; } =
        new List<ScenarioDefinition>
        {
            new("demo",
                "28 realistic inquiries across all five statuses, spanning two pages; a few have null phone/location/message.",
                () => Demo),
            new("single-page",
                "6 inquiries (under one page) with mixed statuses — exercises the no-pagination layout.",
                () => [.. Demo.Take(6)]),
            new("all-new",
                "12 untouched inquiries, all New — a fresh, unworked triage inbox.",
                () => [.. Demo.Take(12).Select(seed => seed with { Status = Status.New })]),
            new("pagination",
                "45 generated inquiries spanning three pages to stress paging and sorting.",
                () => GenerateLoad(45)),
            new("missing-inquiries",
                "26 inquiries for the troubleshooting demo: a worked backlog (Contacted/Pending/Registered/Closed) plus several recent New rows a non-'All' filter hides, one resubmitted duplicate email, and enough volume to push older rows onto page 2.",
                BuildMissingInquiries),
            new("empty",
                "No inquiries — the empty-state UI.",
                () => []),
        }
        .ToDictionary(scenario => scenario.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Listing metadata for the catalog (name, description, size).</summary>
    public static IReadOnlyList<ScenarioInfo> Infos { get; } =
        [.. All.Values.Select(scenario => new ScenarioInfo(scenario.Name, scenario.Description, scenario.Build().Count))];

    /// <summary>
    /// The troubleshooting-demonstration dataset (§ missing-inquiry runbook). Tuned for
    /// SQLite and the default page size (<see cref="InquiryListQuery.DefaultPageSize"/> = 20,
    /// sort <c>createdDateDesc</c>). The seeder backdates rows by lifecycle stage, so the
    /// recent <see cref="Status.New"/> rows sort to the top: with a non-<c>All</c> status
    /// filter active, a staffer still sees a full-looking worked backlog while those recent
    /// New "missing" inquiries are hidden (the display-layer cause, C4). Two rows share one
    /// email (a visitor who resubmitted after a timeout) so the duplicate-email reconciliation
    /// returns a real group (C5 / non-idempotent intake), and 26 total rows push the oldest
    /// onto page 2.
    /// </summary>
    private static IReadOnlyList<SeedInquiry> BuildMissingInquiries() =>
    [
        // Recent New rows — the "missing" inquiries a non-'All' filter hides.
        new("Hannah", "Becker", "hannah.becker@example.com", Course.IntensiveReformer, Status.New,
            Phone: "+1-416-555-0611", PreferredLocation: "Toronto",
            Message: "Keen to start the reformer intensive this fall — which cohorts still have space?"),
        // The same visitor resubmitting after the page appeared to fail: same email, a
        // second stored row (intake is not idempotent, C5).
        new("Hannah", "Becker", "hannah.becker@example.com", Course.IntensiveReformer, Status.New,
            Phone: "+1-416-555-0611", PreferredLocation: "Toronto",
            Message: "Resending — the form seemed to time out the first time. Still hoping to join the fall reformer intensive."),
        new("Diego", "Fuentes", "diego.fuentes@example.com", Course.TotalBarreFoundation, Status.New,
            PreferredLocation: "Miami",
            Message: "Do you have Total Barre foundation dates before the new year?"),
        new("Priya", "Nair", "priya.nair@example.com", Course.ZengaMatReformer, Status.New,
            Phone: "+1-604-555-0640", PreferredLocation: "Vancouver",
            Message: "Interested in ZEN•GA for my studio team — could you send the syllabus?"),
        new("Tomas", "Berg", "tomas.berg@example.com", Course.HaloTrainerFundamentals, Status.New,
            Message: "Is the Halo Trainer course open to instructors still completing certification?"),

        // Worked backlog — Contacted.
        new("Nadia", "Haddad", "nadia.haddad@example.com", Course.InjuriesAndSpecialPopulations, Status.Contacted,
            Phone: "+1-514-555-0622", PreferredLocation: "Montreal",
            Message: "Do you offer payment plans for the special populations course?"),
        new("Owen", "Clarke", "owen.clarke@example.com", Course.CoreAthleticConditioning, Status.Contacted,
            Phone: "+1-403-555-0633", PreferredLocation: "Calgary",
            Message: "Strength coach wanting to add conditioning programming for my athletes."),
        new("Sara", "Lindqvist", "sara.lindqvist@example.com", Course.TotalBarreFoundation, Status.Contacted,
            PreferredLocation: "London",
            Message: "Comparing Total Barre dates between the London and Toronto host sites."),
        new("Marcus", "Wright", "marcus.wright@example.com", Course.IntensiveMatPlus, Status.Contacted,
            Phone: "+1-312-555-0644", PreferredLocation: "Chicago",
            Message: "What are the prerequisites for the Intensive Mat-Plus certification?"),
        new("Leila", "Amini", "leila.amini@example.com", Course.PrenatalPilates, Status.Contacted,
            Phone: "+1-647-555-0655", PreferredLocation: "Toronto",
            Message: "Interested in the prenatal course — is it open to newly certified instructors?"),

        // Worked backlog — Pending.
        new("Victor", "Moreau", "victor.moreau@example.com", Course.RehabReformer, Status.Pending,
            Phone: "+33-1-5555-0666", PreferredLocation: "Montreal",
            Message: "Physiotherapist — waiting on clinic approval before I register for reformer rehab."),
        new("Aiko", "Sato", "aiko.sato@example.com", Course.ZengaMatReformer, Status.Pending,
            Phone: "+81-3-5555-0677", PreferredLocation: "Tokyo",
            Message: "Please confirm the ZEN•GA course is taught in English before I commit."),
        new("Ruth", "Mensah", "ruth.mensah@example.com", Course.FunctionalAnatomy, Status.Pending,
            PreferredLocation: "New York",
            Message: "Holding a place while I finalize my professional-development budget."),
        new("Cole", "Robinson", "cole.robinson@example.com", Course.PilatesForGolf, Status.Pending,
            Phone: "+1-323-555-0688", PreferredLocation: "Los Angeles",
            Message: "Coaching a golf team and want sport-specific programming — what are the next dates?"),
        new("Ingrid", "Solberg", "ingrid.solberg@example.com", Course.HaloTrainerFundamentals, Status.Pending,
            Message: "Waiting to hear back on group rates for three instructors."),

        // Worked backlog — Registered.
        new("Daniela", "Costa", "daniela.costa@example.com", Course.IntensiveMatPlus, Status.Registered,
            Phone: "+55-11-95555-0699", PreferredLocation: "Miami",
            Message: "Excited to start — is there pre-reading before the Intensive Mat-Plus?"),
        new("Peter", "Novak", "peter.novak@example.com", Course.IntensiveReformer, Status.Registered,
            Phone: "+420-2-5555-0700", PreferredLocation: "Berlin",
            Message: "Do you provide the reformer course materials in German?"),
        new("Grace", "Adeyemi", "grace.adeyemi@example.com", Course.InjuriesAndSpecialPopulations, Status.Registered,
            Phone: "+1-212-555-0711", PreferredLocation: "New York",
            Message: "I work with post-surgical clients — looking forward to the special populations cohort."),
        new("Sam", "Whitfield", "sam.whitfield@example.com", Course.CoreAthleticConditioning, Status.Registered,
            PreferredLocation: "Sydney",
            Message: "Registered for conditioning — can you confirm the venue address?"),
        new("Yara", "Khalil", "yara.khalil@example.com", Course.TotalBarreFoundation, Status.Registered,
            Phone: "+971-4-5555-0722", PreferredLocation: "Dubai",
            Message: "Opening a boutique studio — thrilled to certify on Total Barre."),

        // Worked backlog — Closed.
        new("Elena", "Petrova", "elena.petrova@example.com", Course.RehabReformer, Status.Closed,
            PreferredLocation: "London",
            Message: "Physiotherapist asking about upcoming reformer rehab dates in the UK."),
        new("Jack", "Sullivan", "jack.sullivan@example.com", Course.IntensiveCadillacChairBarrels, Status.Closed,
            Phone: "+1-416-555-0733", PreferredLocation: "Toronto",
            Message: "What are the prerequisites for the Cadillac, Chair & Barrels intensive?"),
        new("Mei", "Lin", "mei.lin@example.com", Course.ZengaMatReformer, Status.Closed,
            Phone: "+65-5555-0744", PreferredLocation: "Singapore",
            Message: "An advisor recommended ZEN•GA for my studio — could you send details?"),
        new("Andre", "Dumas", "andre.dumas@example.com", Course.CoreAthleticConditioning, Status.Closed,
            Phone: "+1-514-555-0755", PreferredLocation: "Montreal",
            Message: "Personal trainer wanting to expand my scope of practice."),
        new("Fatima", "Rahim", "fatima.rahim@example.com", Course.FunctionalAnatomy, Status.Closed,
            PreferredLocation: "Boston",
            Message: "Comparing anatomy-for-movement courses — what sets yours apart?"),
        new("Liam", "Foster", "liam.foster@example.com", Course.PrenatalPilates, Status.Closed,
            Phone: "+61-2-5555-0766", PreferredLocation: "Sydney",
            Message: "Following up after the info session about the prenatal course."),
    ];

    /// <summary>
    /// Deterministically generates <paramref name="count"/> inquiries by cycling
    /// through name, course, location, and status pools — used by scenarios that
    /// need volume (pagination) rather than curated copy.
    /// </summary>
    private static IReadOnlyList<SeedInquiry> GenerateLoad(int count)
    {
        string[] firstNames = ["Alex", "Sam", "Jordan", "Taylor", "Casey", "Riley", "Morgan", "Jamie", "Quinn", "Avery"];
        string[] lastNames = ["Reyes", "Novak", "Haddad", "Ivanov", "Costa", "Fischer", "Larsen", "Mendez", "Park", "Blake"];
        string[] courses = Course.All;
        string[] locations =
        [
            "Toronto", "Vancouver", "Montreal", "New York", "Los Angeles",
            "London", "Sydney", "Singapore", "Tokyo", "Dubai",
        ];
        Status[] statuses = [Status.New, Status.New, Status.Contacted, Status.Pending, Status.Registered, Status.Closed];

        var rows = new List<SeedInquiry>(count);
        for (var i = 0; i < count; i++)
        {
            var first = firstNames[i % firstNames.Length];
            var last = lastNames[(i * 7) % lastNames.Length];
            rows.Add(new SeedInquiry(
                first,
                last,
                $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{i + 1}@example.com",
                courses[i % courses.Length],
                statuses[i % statuses.Length],
                Phone: i % 3 == 0 ? null : $"+1-416-555-{1000 + i:D4}",
                PreferredLocation: i % 5 == 0 ? null : locations[i % locations.Length],
                Message: i % 2 == 0 ? $"Generated load-test inquiry #{i + 1}." : null));
        }

        return rows;
    }
}
