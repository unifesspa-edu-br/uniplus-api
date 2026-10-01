namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Opção que o processo declara para um fato cuja fonte dos valores é o processo — por exemplo,
/// as edições do ENEM aceitas ou as cidades de prova —, congelada no edital (issue #1619). Os
/// fatos de atendimento especializado não usam esta entidade: as suas opções vêm da oferta de
/// atendimento (<see cref="OfertaAtendimentoEspecializado"/>).
/// </summary>
/// <remarks>
/// <see cref="EntityBase"/> puro (sem soft-delete): ver justificativa em <see cref="EtapaProcesso"/>.
/// </remarks>
public sealed class OpcaoDeclaradaFato : EntityBase
{
    public const int FatoCodigoMaxLength = FormaDoItem.FatoCodigoMaxLength;
    public const int CodigoMaxLength = 60;
    public const int RotuloMaxLength = 300;

    public Guid ProcessoSeletivoId { get; private set; }

    public string FatoCodigo { get; private set; } = string.Empty;

    public string Codigo { get; private set; } = string.Empty;

    public string Rotulo { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    private OpcaoDeclaradaFato() { }

    public static Result<OpcaoDeclaradaFato> Criar(string fatoCodigo, string codigo, string rotulo, int ordem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);

        List<FieldError> erros = [];
        string codigoNormalizado = codigo?.Trim() ?? string.Empty;
        string rotuloNormalizado = rotulo?.Trim() ?? string.Empty;

        if (codigoNormalizado.Length is 0 or > CodigoMaxLength)
        {
            erros.Add(new("codigo", new DomainError(
                OpcaoDeclaradaFatoErrorCodes.CodigoInvalido,
                $"O código da opção é obrigatório e tem no máximo {CodigoMaxLength} caracteres.")));
        }

        if (rotuloNormalizado.Length is 0 or > RotuloMaxLength)
        {
            erros.Add(new("rotulo", new DomainError(
                OpcaoDeclaradaFatoErrorCodes.RotuloInvalido,
                $"O rótulo da opção é obrigatório e tem no máximo {RotuloMaxLength} caracteres.")));
        }

        if (erros.Count > 0)
        {
            return Result<OpcaoDeclaradaFato>.ValidationFailure(erros);
        }

        return Result<OpcaoDeclaradaFato>.Success(new OpcaoDeclaradaFato
        {
            FatoCodigo = fatoCodigo.Trim(),
            Codigo = codigoNormalizado,
            Rotulo = rotuloNormalizado,
            Ordem = ordem,
        });
    }

    internal void VincularProcesso(Guid processoSeletivoId) => ProcessoSeletivoId = processoSeletivoId;

    /// <summary>Repõe o rótulo e a ordem congelados na restauração de uma versão publicada.</summary>
    internal void ReporConteudo(string rotulo, int ordem)
    {
        Rotulo = rotulo;
        Ordem = ordem;
    }
}
