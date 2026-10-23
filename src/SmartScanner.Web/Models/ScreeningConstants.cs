namespace SmartScanner.Web.Models;

public static class SubmissionStatuses
{
    public const string Received = "RECEIVED";
    public const string Processing = "PROCESSING";
    public const string Passed = "PASSED";
    public const string Failed = "FAILED";
    public const string SystemError = "SYSTEM_ERROR";
}

public static class CheckStatuses
{
    public const string Pass = "PASS";
    public const string Fail = "FAIL";
    public const string Skipped = "SKIPPED";
    public const string Error = "ERROR";
}

public static class ReasonCodes
{
    public const string MetadataIncomplete = "METADATA_INCOMPLETE";
    public const string AssignmentNotFound = "ASSIGNMENT_NOT_FOUND";
    public const string InvalidFileExtension = "INVALID_FILE_EXTENSION";
    public const string ArchiveEmpty = "ARCHIVE_EMPTY";
    public const string ArchiveTooLarge = "ARCHIVE_TOO_LARGE";
    public const string FilenameMismatch = "FILENAME_MISMATCH";
    public const string ArchiveInvalid = "ARCHIVE_INVALID";
    public const string ArchiveFileCountLimit = "ARCHIVE_FILE_COUNT_LIMIT";
    public const string ArchiveEntrySizeLimit = "ARCHIVE_ENTRY_SIZE_LIMIT";
    public const string ArchiveUncompressedSizeLimit = "ARCHIVE_UNCOMPRESSED_SIZE_LIMIT";
    public const string ArchiveUnsafePath = "ARCHIVE_UNSAFE_PATH";
    public const string ArchiveSymlinkNotAllowed = "ARCHIVE_SYMLINK_NOT_ALLOWED";
    public const string ArchiveExtractionFailed = "ARCHIVE_EXTRACTION_FAILED";
    public const string CsFileNotFound = "CS_FILE_NOT_FOUND";
    public const string CsprojNotFound = "CSPROJ_NOT_FOUND";
    public const string MultipleCsprojFound = "MULTIPLE_CSPROJ_FOUND";
    public const string AdministrativeScanError = "ADMINISTRATIVE_SCAN_ERROR";
    public const string ScreeningPipelineError = "SCREENING_PIPELINE_ERROR";
    public const string RunnerUnavailable = "RUNNER_UNAVAILABLE";
    public const string BuildTimeout = "BUILD_TIMEOUT";
    public const string BuildFailed = "BUILD_FAILED";
    public const string AttendanceNotMapped = "ATTENDANCE_NOT_MAPPED";
    public const string FunctionalTestFailed = "FUNCTIONAL_TEST_FAILED";
    public const string FunctionalTestTimeout = "FUNCTIONAL_TEST_TIMEOUT";
    public const string ProgramCrashed = "PROGRAM_CRASHED";
    public const string OutputMismatch = "OUTPUT_MISMATCH";
}
