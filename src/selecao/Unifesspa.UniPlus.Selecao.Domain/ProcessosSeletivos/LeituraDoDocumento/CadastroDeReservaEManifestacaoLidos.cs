namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>
/// O cadastro de reserva e a manifestação de interesse que a leitura do documento entregou como declarações, e a origem que guarda
/// as partes que a leitura recusou. A conferência é a mesma da gravação por dimensão: o bloco recusado fica sem declaração, o
/// cadastro de reserva nulo é o bloco recusado, e a manifestação nula é a não prevista, distinta da recusada pela origem
/// (<see cref="OrigemNoDocumento.TemParteRecusadaEm"/> no campo da manifestação).
/// </summary>
public sealed record CadastroDeReservaEManifestacaoLidos(
    CadastroDeReservaPorChamada? CadastroDeReserva,
    ManifestacaoDeInteresse? ManifestacaoDeInteresse,
    OrigemNoDocumento Origem)
{
    /// <summary>Confere o cadastro de reserva e a manifestação do documento pelas regras de domínio e acumula as recusas dos dois blocos.</summary>
    public static CadastroDeReservaEManifestacaoLidos Ler(DeclaracoesDoDocumento declaracoes)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);

        Result<CadastroDeReservaPorChamada> cadastro = CadastroDeReservaPorChamada.Declarar(declaracoes.CadastroDeReserva);
        Result<ManifestacaoDeInteresse?> manifestacao = DeclaracaoDaManifestacaoDeInteresse.Declarar(declaracoes.ManifestacaoDeInteresse);

        return new(
            cadastro.IsSuccess ? cadastro.Value : null,
            manifestacao.IsSuccess ? manifestacao.Value : null,
            OrigemNoDocumento.SoComPartesRecusadas(
                [.. Recusadas(cadastro.Errors, CadastroDeReservaPorChamada.Campo), .. Recusadas(manifestacao.Errors, DeclaracaoDaManifestacaoDeInteresse.Campo)]));
    }

    private static IEnumerable<ParteRecusada> Recusadas(IReadOnlyList<FieldError> causas, string campoDoBloco) =>
        causas.Select(causa => ParteRecusada.DoBloco(campoDoBloco, causa));
}
