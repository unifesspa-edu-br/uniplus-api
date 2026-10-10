namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Domain.Entities;
using Domain.Errors;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O veredito de submissão (issue #1859) — NOVO: diferente da validação estrutural do rascunho
/// (<c>SalvarInscricaoCommandHandler</c>), aqui a ausência de resposta a um item visível e
/// obrigatório, um impedimento verdadeiro, um grupo fora do mínimo/máximo ou sem o candidato, e um
/// termo obrigatório não aceito (ou aceito com versão/hash diferente da versão fixada) TODOS
/// recusam a submissão — nada disso é tolerado no rascunho.
/// </summary>
/// <remarks>
/// Recebe a avaliação já pronta (<see cref="AvaliacaoFormulario"/>), calculada com
/// <c>EtapasConcluidas</c> = todas as etapas do recorte: a submissão É o candidato declarando que
/// terminou, então o veredito avalia como se tudo estivesse concluído — um campo opcional sem
/// resposta deixa de ser pendência (<see cref="EstadoFato.Indeterminado"/>) e passa a
/// <see cref="EstadoFato.NaoInformado"/>, que não pesa; só o obrigatório sem resposta permanece
/// indeterminado.
/// </remarks>
public static class VeredictoDeSubmissao
{
    public static IReadOnlyList<FieldError> Avaliar(
        AvaliacaoFormulario avaliacao,
        IReadOnlyList<TermoExigidoFormulario> termosExigidos,
        IReadOnlyList<AceiteDeTermo> aceites)
    {
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(termosExigidos);
        ArgumentNullException.ThrowIfNull(aceites);

        List<FieldError> recusas = [.. ProblemasDosItens(avaliacao.Itens, avaliacao.Fatos, caminho: null)];

        foreach (AvaliacaoGrupo grupo in avaliacao.Grupos)
        {
            if (grupo.Visivel != Ternario.Verdadeiro)
            {
                continue;
            }

            if (!grupo.ContagemValida)
            {
                recusas.Add(Recusa(grupo.Codigo, InscricaoErrorCodes.GrupoContagemInvalida, "O número de ocorrências do grupo não respeita o mínimo ou o máximo exigido."));
            }

            if (!grupo.OcorrenciaDoCandidatoValida)
            {
                recusas.Add(Recusa(grupo.Codigo, InscricaoErrorCodes.GrupoCandidatoAusente, "O grupo inclui o candidato, mas nenhuma ocorrência o representa."));
            }

            if (grupo.Obrigatorio == Ternario.Verdadeiro && grupo.Estado == EstadoFato.Indeterminado)
            {
                recusas.Add(Recusa(grupo.Codigo, InscricaoErrorCodes.ItemObrigatorioAusente, $"O grupo '{grupo.Codigo}' é obrigatório e não tem ocorrência nenhuma."));
            }

            HashSet<string> idsVistos = new(StringComparer.Ordinal);
            foreach (AvaliacaoOcorrencia ocorrencia in grupo.Ocorrencias)
            {
                if (!idsVistos.Add(ocorrencia.Id))
                {
                    recusas.Add(Recusa($"{grupo.Codigo}.{ocorrencia.Id}", InscricaoErrorCodes.GrupoIdDuplicado, $"Duas ocorrências do grupo '{grupo.Codigo}' repetem o id '{ocorrencia.Id}'."));
                }

                recusas.AddRange(ProblemasDosItens(ocorrencia.Itens, ocorrencia.Fatos, caminho: $"{grupo.Codigo}.{ocorrencia.Id}"));
            }
        }

        foreach (AvaliacaoTermo termo in avaliacao.Termos)
        {
            if (termo.Visivel != Ternario.Verdadeiro || termo.Obrigatorio != Ternario.Verdadeiro)
            {
                continue;
            }

            // avaliacao.Termos usa o código PREFIXADO pela finalidade (DefinicaoDoProcesso.CodigoDoTermo) —
            // o mesmo que a apresentação do formulário já expõe (ProjecaoDoFormularioRenderizavel.Termo).
            // termosExigidos e aceites usam o código NU, que é o que o candidato conhece e envia; a
            // recusa é sobre o aceite, então sai nele.
            TermoExigidoFormulario? exigido = termosExigidos.FirstOrDefault(t =>
                string.Equals(DefinicaoDoProcesso.CodigoDoTermo(t.Finalidade, t.Codigo), termo.Codigo, StringComparison.Ordinal));
            AceiteDeTermo? aceite = exigido is null
                ? null
                : aceites.FirstOrDefault(a => string.Equals(a.Codigo, exigido.Codigo, StringComparison.Ordinal));

            bool aceitoNaVersaoFixada = exigido is not null && aceite is not null
                && aceite.VersaoId == exigido.VersaoId
                && string.Equals(aceite.HashVersao, exigido.HashVersao, StringComparison.Ordinal);

            if (!aceitoNaVersaoFixada)
            {
                string campo = exigido?.Codigo ?? termo.Codigo;
                recusas.Add(Recusa(campo, InscricaoErrorCodes.TermoNaoAceito, $"O termo '{campo}' é obrigatório e não foi aceito na versão fixada da inscrição."));
            }
        }

        return recusas;
    }

    /// <summary>
    /// <see cref="FormularioPortavel.Avaliar"/> descarta em silêncio, via <c>RespostaDeCampo.DentroDaOferta</c>,
    /// a resposta de um fato ofertado cujo código não pertence à oferta vigente — o item some da
    /// avaliação como se não houvesse resposta, e a submissão recusaria no máximo por obrigatoriedade
    /// (nunca por fato de o código ser inválido, e nem recusaria nada se o item for opcional). A
    /// submissão precisa da recusa explícita: por isso esta checagem roda sobre as respostas BRUTAS,
    /// antes de qualquer filtro, e sempre junto de <see cref="Avaliar"/> — nunca no lugar dele.
    /// </summary>
    public static IReadOnlyList<FieldError> RecusasDeOpcaoForaDaOferta(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas)
    {
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(ofertas);

        List<FieldError> recusas = [.. ForaDaOferta(respostas, ofertas, caminho: null)];
        foreach ((string codigoGrupo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in grupos)
        {
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                recusas.AddRange(ForaDaOferta(ocorrencia.Respostas, ofertas, caminho: $"{codigoGrupo}.{ocorrencia.Id}"));
            }
        }

        return recusas;
    }

    private static IEnumerable<FieldError> ForaDaOferta(
        IReadOnlyDictionary<string, JsonElement> respostas, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas, string? caminho)
    {
        foreach ((string fatoCodigo, JsonElement valor) in respostas)
        {
            if (!ofertas.TryGetValue(fatoCodigo, out IReadOnlySet<string>? oferta)
                || RespostaDeCampo.EstaVazia(valor)
                || RespostaDeCampo.Codigos(valor) is not { } codigos
                || codigos.All(oferta.Contains))
            {
                continue;
            }

            yield return Recusa(
                caminho is null ? fatoCodigo : $"{caminho}.{fatoCodigo}",
                InscricaoErrorCodes.RespostaInvalida,
                $"A resposta de '{fatoCodigo}' tem uma opção fora da oferta vigente.");
        }
    }

    private static IEnumerable<FieldError> ProblemasDosItens(
        IReadOnlyList<AvaliacaoItem> itens, IReadOnlyDictionary<string, FatoResolvido> fatos, string? caminho)
    {
        foreach (AvaliacaoItem item in itens)
        {
            if (item.Visivel != Ternario.Verdadeiro)
            {
                continue;
            }

            string campo = caminho is null ? item.FatoCodigo : $"{caminho}.{item.FatoCodigo}";

            if (item.Impedido == Ternario.Verdadeiro)
            {
                yield return Recusa(campo, InscricaoErrorCodes.Impedimento, $"A resposta de '{item.FatoCodigo}' impede a submissão desta inscrição.");
            }

            if (item.RestricoesVioladas.Count > 0)
            {
                yield return Recusa(campo, InscricaoErrorCodes.RespostaInvalida, $"A resposta de '{item.FatoCodigo}' não atende a uma restrição do formulário.");
            }

            if (fatos.TryGetValue(item.FatoCodigo, out FatoResolvido? resolvido) && resolvido.Estado == EstadoFato.Indeterminado)
            {
                yield return Recusa(campo, InscricaoErrorCodes.ItemObrigatorioAusente, $"O item '{item.FatoCodigo}' é obrigatório e está sem resposta.");
            }
        }
    }

    private static FieldError Recusa(string campo, string codigo, string mensagem) => new(campo, new DomainError(codigo, mensagem));
}
