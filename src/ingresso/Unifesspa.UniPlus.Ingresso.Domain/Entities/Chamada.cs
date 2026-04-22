namespace Unifesspa.UniPlus.Ingresso.Domain.Entities;

using Unifesspa.UniPlus.Ingresso.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

public sealed class Chamada : EntityBase
{
    public Guid EditalId { get; private set; }
    public int Numero { get; private set; }
    public StatusChamada Status { get; private set; }
    public DateTimeOffset DataPublicacao { get; private set; }
    public DateTimeOffset PrazoManifestacao { get; private set; }

    private readonly List<Convocacao> _convocacoes = [];
    public IReadOnlyCollection<Convocacao> Convocacoes => _convocacoes.AsReadOnly();

    private Chamada() { }

    public static Chamada Criar(Guid editalId, int numero, DateTimeOffset dataPublicacao, DateTimeOffset prazoManifestacao)
    {
        return new Chamada
        {
            EditalId = editalId,
            Numero = numero,
            Status = StatusChamada.Agendada,
            DataPublicacao = dataPublicacao,
            PrazoManifestacao = prazoManifestacao
        };
    }

    public void AdicionarConvocacao(Convocacao convocacao) =>
        _convocacoes.Add(convocacao);

    public void Iniciar() => Status = StatusChamada.EmAndamento;

    public void Concluir() => Status = StatusChamada.Concluida;
}
