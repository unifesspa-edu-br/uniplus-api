namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Idade máxima de emissão de um <see cref="Entities.DocumentoExigido"/> (Story #554,
/// PR-d, issue #893) — regra opcional (0..1) sobre o próprio ARQUIVO apresentado (ex.:
/// "comprovante de residência emitido há no máximo 90 dias"), distinta de
/// <see cref="ReferenciaTemporalFatos"/> (PR-b), que ancora a idade do CANDIDATO
/// (<c>FAIXA_ETARIA</c>) no nível do processo, não da exigência, e explicitamente exclui
/// submissão como âncora.
/// </summary>
/// <remarks>
/// <para>
/// Coerência tudo-nulo OU completo (N-I01, mesmo espírito de <see cref="ReferenciaTemporalFatos"/>):
/// <see cref="Valor"/>/<see cref="Unidade"/>/<see cref="ReferenciaTipo"/> são os três
/// primeiros campos — presentes juntos, ou ausentes juntos. <see cref="Enums.ReferenciaTipoIdadeEmissao.DataEspecifica"/>
/// exige <see cref="Data"/>; <see cref="Enums.ReferenciaTipoIdadeEmissao.InicioFase"/>/
/// <see cref="Enums.ReferenciaTipoIdadeEmissao.FimFase"/> exigem <see cref="ReferenciaFaseId"/>;
/// <see cref="Enums.ReferenciaTipoIdadeEmissao.FimInscricao"/>/
/// <see cref="Enums.ReferenciaTipoIdadeEmissao.DataSubmissao"/> não usam nenhum dos dois
/// campos opcionais (resolvidos em runtime de coleta, fora de escopo desta Story).
/// </para>
/// <para>
/// <b>Aviso, não bloqueio de presença</b> (issue #893 §1): esta regra nunca impede um
/// documento de ser aceito como apresentação — ela sinaliza. A avaliação em runtime
/// (comparar a emissão real do arquivo contra esta regra congelada) é fora de escopo.
/// </para>
/// </remarks>
public sealed record IdadeMaximaEmissao
{
    private IdadeMaximaEmissao(
        int valor, UnidadeIdade unidade, ReferenciaTipoIdadeEmissao referenciaTipo, DateOnly? data, Guid? referenciaFaseId)
    {
        Valor = valor;
        Unidade = unidade;
        ReferenciaTipo = referenciaTipo;
        Data = data;
        ReferenciaFaseId = referenciaFaseId;
    }

    public int Valor { get; }

    public UnidadeIdade Unidade { get; }

    public ReferenciaTipoIdadeEmissao ReferenciaTipo { get; }

    /// <summary>Só presente quando <see cref="ReferenciaTipo"/> é <see cref="Enums.ReferenciaTipoIdadeEmissao.DataEspecifica"/>.</summary>
    public DateOnly? Data { get; }

    /// <summary>Só presente quando <see cref="ReferenciaTipo"/> é <see cref="Enums.ReferenciaTipoIdadeEmissao.InicioFase"/> ou <see cref="Enums.ReferenciaTipoIdadeEmissao.FimFase"/>.</summary>
    public Guid? ReferenciaFaseId { get; }

    /// <summary>
    /// <see langword="null"/> tudo-nulo (regra ausente — a exigência não tem idade máxima
    /// de emissão) é sucesso, não erro: a ausência de <paramref name="valor"/>/
    /// <paramref name="unidade"/>/<paramref name="referenciaTipo"/> é um estado válido.
    /// </summary>
    public static Result<IdadeMaximaEmissao?> Criar(
        int? valor, UnidadeIdade? unidade, ReferenciaTipoIdadeEmissao? referenciaTipo, DateOnly? data, Guid? referenciaFaseId)
    {
        bool nenhumDosTresPresente = valor is null && unidade is null && referenciaTipo is null;
        if (nenhumDosTresPresente)
        {
            if (data is not null || referenciaFaseId is not null)
            {
                return Result<IdadeMaximaEmissao?>.Failure(new DomainError(
                    "IdadeMaximaEmissao.CamposIncoerentesComAusencia",
                    "Data e a fase âncora só são aceitas quando a idade máxima de emissão está definida (Valor, Unidade e ReferenciaTipo)."));
            }

            return Result<IdadeMaximaEmissao?>.Success(null);
        }

        if (valor is null || unidade is null || referenciaTipo is null)
        {
            return Result<IdadeMaximaEmissao?>.Failure(new DomainError(
                "IdadeMaximaEmissao.CamposIncompletos",
                "Valor, Unidade e ReferenciaTipo devem estar todos presentes, ou todos ausentes."));
        }

        if (valor <= 0)
        {
            return Result<IdadeMaximaEmissao?>.Failure(new DomainError(
                "IdadeMaximaEmissao.ValorInvalido",
                "O valor da idade máxima de emissão deve ser maior que zero."));
        }

        bool exigeData = referenciaTipo == ReferenciaTipoIdadeEmissao.DataEspecifica;
        if (exigeData != data.HasValue)
        {
            return Result<IdadeMaximaEmissao?>.Failure(new DomainError(
                "IdadeMaximaEmissao.DataIncoerenteComTipo",
                exigeData
                    ? "DATA_ESPECIFICA exige a data."
                    : "A data só é aceita quando o tipo é DATA_ESPECIFICA."));
        }

        bool exigeFase = referenciaTipo is ReferenciaTipoIdadeEmissao.InicioFase or ReferenciaTipoIdadeEmissao.FimFase;
        if (exigeFase != referenciaFaseId.HasValue)
        {
            return Result<IdadeMaximaEmissao?>.Failure(new DomainError(
                "IdadeMaximaEmissao.FaseIncoerenteComTipo",
                exigeFase
                    ? "INICIO_FASE/FIM_FASE exigem a fase âncora."
                    : "A fase âncora só é aceita quando o tipo é INICIO_FASE ou FIM_FASE."));
        }

        return Result<IdadeMaximaEmissao?>.Success(
            new IdadeMaximaEmissao(valor.Value, unidade.Value, referenciaTipo.Value, data, referenciaFaseId));
    }
}
