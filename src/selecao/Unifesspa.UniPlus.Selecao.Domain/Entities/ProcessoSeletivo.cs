namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.SharedKernel.Domain.Entities;

public sealed class ProcessoSeletivo : EntityBase
{
    public Guid EditalId { get; private set; }
    public string CodigoCurso { get; private set; } = string.Empty;
    public string NomeCurso { get; private set; } = string.Empty;
    public string Campus { get; private set; } = string.Empty;
    public int TotalVagas { get; private set; }
    public string? Turno { get; private set; }

    private ProcessoSeletivo() { }

    public static ProcessoSeletivo Criar(Guid editalId, string codigoCurso, string nomeCurso, string campus, int totalVagas, string? turno = null)
    {
        return new ProcessoSeletivo
        {
            EditalId = editalId,
            CodigoCurso = codigoCurso,
            NomeCurso = nomeCurso,
            Campus = campus,
            TotalVagas = totalVagas,
            Turno = turno
        };
    }
}
