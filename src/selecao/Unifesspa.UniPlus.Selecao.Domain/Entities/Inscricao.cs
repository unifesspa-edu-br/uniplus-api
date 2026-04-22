namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class Inscricao : EntityBase
{
    public Guid CandidatoId { get; private set; }
    public Guid EditalId { get; private set; }
    public ModalidadeConcorrencia Modalidade { get; private set; }
    public StatusInscricao Status { get; private set; }
    public string? CodigoCursoPrimeiraOpcao { get; private set; }
    public string? CodigoCursoSegundaOpcao { get; private set; }
    public bool ListaEspera { get; private set; }
    public string? NumeroInscricao { get; private set; }

    private Inscricao() { }

    public static Result<Inscricao> Criar(Guid candidatoId, Guid editalId, ModalidadeConcorrencia modalidade, string codigoCursoPrimeiraOpcao)
    {
        var inscricao = new Inscricao
        {
            CandidatoId = candidatoId,
            EditalId = editalId,
            Modalidade = modalidade,
            Status = StatusInscricao.Rascunho,
            CodigoCursoPrimeiraOpcao = codigoCursoPrimeiraOpcao,
            NumeroInscricao = GerarNumeroInscricao()
        };

        return Result<Inscricao>.Success(inscricao);
    }

    public Result<Inscricao> Confirmar()
    {
        if (Status != StatusInscricao.Rascunho)
            return Result<Inscricao>.Failure(new DomainError("Inscricao.StatusInvalido", "Somente inscrições em rascunho podem ser confirmadas."));

        Status = StatusInscricao.Confirmada;
        AddDomainEvent(new InscricaoRealizadaEvent(Id, CandidatoId, EditalId));
        return Result<Inscricao>.Success(this);
    }

    public void Cancelar()
    {
        Status = StatusInscricao.Cancelada;
    }

    public void DefinirSegundaOpcao(string codigoCurso) =>
        CodigoCursoSegundaOpcao = codigoCurso;

    public void OptarPorListaEspera() => ListaEspera = true;

    private static string GerarNumeroInscricao() =>
        $"{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
}
