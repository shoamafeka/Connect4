namespace Connect4_Client
{
    // Minimal player shape mirrored from the server API.
    // Strings initialized to avoid NRT warnings; no behavior change.
    internal class PlayerDto
    {
        public int Id { get; set; }          // Internal DB PK on the server
        public int PlayerId { get; set; }    // External ID (1..1000)
        public string FirstName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }
}
