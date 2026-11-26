namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Kernel.Domain.OfertasDeCurso;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a camada de aplicação resolve nos cadastros sobre a oferta e entrega ao domínio junto da
/// distribuição declarada: o teto de vagas anuais autorizadas, o rol de modalidades que a regra admite, o
/// grupo de área do ENEM do curso e o que nomeia a oferta para o candidato. Cada fato ausente é legítimo e
/// não impõe restrição.
/// </summary>
public sealed record FatosDaOfertaNoCadastro
{
    /// <summary>Nenhum fato do cadastro: a oferta não declara teto, a regra não restringe o rol e não há grupo nem nome.</summary>
    public static FatosDaOfertaNoCadastro Nenhum { get; } = new();

    /// <summary>O teto de vagas anuais autorizadas no e-MEC; nulo quando a oferta não o declara.</summary>
    public int? VagasAnuaisAutorizadas { get; init; }

    /// <summary>Os códigos que a regra de distribuição reconhece; nulo ou vazio quando ela não restringe o rol.</summary>
    public IReadOnlyCollection<string>? ModalidadesAdmitidas { get; init; }

    /// <summary>Código e rótulo do grupo de área do ENEM como o cadastro os entrega; nulo quando o curso não o declara.</summary>
    public (string? Codigo, string? Rotulo)? GrupoAreaEnem { get; init; }

    /// <summary>O que nomeia a oferta para o candidato, como o cadastro o entrega.</summary>
    public CopiaDaOfertaDeCurso? OfertaCurso { get; init; }
}
