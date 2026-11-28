namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// As declarações do próprio processo lidas do envelope, antes dos demais blocos, cada uma pela leitura do seu assunto. Uma
/// leitura que recusa deixa a declaração nula, e o primeiro desvio fica no <see cref="LeitorEnvelope"/>.
/// </summary>
internal static class LeituraDasDeclaracoesDoProcesso
{
    internal static DeclaracoesDoProcessoLidas Ler(LeitorEnvelope leitor, JsonObject payload) =>
        new(
            LeituraDaProvaPresencial.Ler(leitor, payload), LeituraDoCadastroDeReserva.Ler(leitor, payload),
            LeituraDaManifestacaoDeInteresse.Ler(leitor, payload), LeituraDaImportacaoDoResultado.Ler(leitor, payload));
}

/// <summary>As declarações do processo lidas do envelope, que passam aos blocos da configuração depois da leitura destes.</summary>
internal sealed record DeclaracoesDoProcessoLidas(
    DeclaracaoDaProvaPresencial? ProvaPresencial,
    CadastroDeReservaPorChamada? CadastroDeReserva,
    ManifestacaoDeInteresse? ManifestacaoDeInteresse,
    DeclaracaoDaImportacaoDoResultado? ImportacaoDoResultado)
{
    internal BlocosDoEnvelope Aplicar(BlocosDoEnvelope blocos) => blocos with
    {
        Configuracao = blocos.Configuracao with
        {
            ProvaPresencial = ProvaPresencial,
            CadastroDeReserva = CadastroDeReserva ?? CadastroDeReservaPorChamada.SemReserva,
            ManifestacaoDeInteresse = ManifestacaoDeInteresse,
            ImportacaoDoResultado = ImportacaoDoResultado,
        },
    };
}
