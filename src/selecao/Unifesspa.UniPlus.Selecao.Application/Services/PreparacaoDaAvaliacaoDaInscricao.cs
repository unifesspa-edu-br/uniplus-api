namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Prepara e faz a avaliação da inscrição fora do envio — no rascunho, na leitura e na consulta do
/// candidato (ADR-0143): mescla o que chegou ao que a inscrição já gravou, apura os derivados do
/// sistema de que a avaliação depende e a entrega à <see cref="AvaliacaoDaInscricao"/>. Nenhuma etapa é
/// dada como concluída, porque o candidato ainda preenche; e o derivado que não se consegue apurar
/// fica indeterminado, sem recusar — quem recusa é o envio.
/// </summary>
public sealed class PreparacaoDaAvaliacaoDaInscricao(IConferenciaEnderecoGeo conferenciaEnderecoGeo)
{
    /// <param name="respostasRecebidas">As respostas que chegaram; o campo ausente vale o que já está gravado.</param>
    /// <param name="gruposRecebidos">As ocorrências que chegaram, por grupo; o grupo ausente vale o que já está gravado.</param>
    /// <param name="envio">O instante em que as respostas seriam gravadas, de que se contam os limites de data relativos a ele.</param>
    public async Task<Result<ResultadoDaAvaliacaoDaInscricao>> AvaliarAsync(
        Inscricao inscricao,
        ContextoDaVersaoFixada contexto,
        IReadOnlyDictionary<string, JsonElement> respostasRecebidas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> gruposRecebidos,
        EnvioDasRespostas envio,
        ArquivosDaInscricao arquivos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(respostasRecebidas);
        ArgumentNullException.ThrowIfNull(gruposRecebidos);
        ArgumentNullException.ThrowIfNull(conferenciaEnderecoGeo);
        ArgumentNullException.ThrowIfNull(arquivos);

        IReadOnlyDictionary<string, JsonElement> respostas = inscricao.Formulario.Preenchimento.RespostasAposMesclar(respostasRecebidas);
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = MesclarGrupos(inscricao, gruposRecebidos);
        Dictionary<string, FatoResolvido> derivados = await ApurarDerivadosAsync(inscricao, contexto, respostasRecebidas, respostas, cancellationToken).ConfigureAwait(false);

        return AvaliarMesclado(contexto, respostas, grupos, derivados, envio, arquivos);
    }

    private static Result<ResultadoDaAvaliacaoDaInscricao> AvaliarMesclado(
        ContextoDaVersaoFixada contexto, IReadOnlyDictionary<string, JsonElement> respostas,
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos, Dictionary<string, FatoResolvido> derivados,
        EnvioDasRespostas envio, ArquivosDaInscricao arquivos)
    {
        return AvaliacaoDaInscricaoPorOpcao.Avaliar(
            contexto,
            EntradaDaAvaliacaoDaInscricao.Montar(
                DerivadosDoSistemaInscricao.ComOsDerivados(contexto.Admitidas.Definicoes), new EntradaDaAvaliacaoDaInscricao(
                respostas, grupos, derivados, new HashSet<string>(StringComparer.Ordinal), envio,
                arquivos.ApresentacoesVerificadas(),
                NaFaseDaInscricao: true)));
    }

    private async Task<Dictionary<string, FatoResolvido>> ApurarDerivadosAsync(
        Inscricao inscricao, ContextoDaVersaoFixada contexto,
        IReadOnlyDictionary<string, JsonElement> respostasRecebidas, IReadOnlyDictionary<string, JsonElement> respostas,
        CancellationToken cancellationToken)
    {
        IReadOnlySet<string> citados = AvaliacaoDaInscricao.DerivadosDoSistemaCitados(contexto.Configuracao);
        bool residenciaDoEnvio = !inscricao.EhRascunho && !TrazEnderecoResidencial(respostasRecebidas);
        Dictionary<string, FatoResolvido> derivados = new(await DerivadosDoSistemaInscricao.ApurarAsync(
            respostas, contexto.DataReferenciaFatos, conferenciaEnderecoGeo,
            residenciaDoEnvio ? citados.Except(DerivadosDoSistemaInscricao.FatosDaResidencia).ToHashSet(StringComparer.Ordinal) : citados,
            cancellationToken).ConfigureAwait(false), StringComparer.Ordinal);
        if (residenciaDoEnvio)
        {
            IncorporarResidenciaGravada(inscricao, citados, derivados);
        }
        return derivados;
    }

    private static void IncorporarResidenciaGravada(Inscricao inscricao, IReadOnlySet<string> citados, Dictionary<string, FatoResolvido> derivados)
    {
        foreach ((string fato, FatoResolvido resolvido) in DerivadosDoSistemaInscricao.ResidenciaGravada(inscricao.Formulario.Derivados, citados))
        {
            derivados[fato] = resolvido;
        }
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> MesclarGrupos(
        Inscricao inscricao, IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaRespondida>> recebidos)
    {
        Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> grupos = InscricaoRequestMapper.ParaAvaliacao(inscricao.Formulario.Preenchimento.Grupos);
        foreach ((string codigo, IReadOnlyList<OcorrenciaRespondida> ocorrencias) in recebidos)
        {
            grupos[codigo] = ocorrencias;
        }
        return grupos;
    }

    private static bool TrazEnderecoResidencial(IReadOnlyDictionary<string, JsonElement> respostasRecebidas) =>
        respostasRecebidas.TryGetValue(DerivadosDoSistemaInscricao.EnderecoResidencial, out JsonElement endereco)
        && endereco.ValueKind != JsonValueKind.Null;
}
