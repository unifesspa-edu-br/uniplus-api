namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O que a definição nova de um fato deixaria de valer nas coisas que o citam, dentro da
/// Configuração (ADR-0136): as regras padrão de outros derivados, os agregados que o apontam como
/// membro, os modelos de formulário e as regras de aplicabilidade das modalidades. Cada citação é
/// conferida contra o catálogo com o fato alterado e contra o catálogo atual: só a recusa que a
/// alteração causa conta, e a citação que já estava fora (fato desativado que o modelo já vinculava,
/// por exemplo) continua como estava. Uma recusa por citação, que lista o que deixaria de valer.
/// </summary>
internal static class ConferenciaDasCitacoesDoFato
{
    public static List<FieldError> Conferir(
        string codigo,
        CatalogoDeFatos antes,
        CatalogoDeFatos depois,
        IReadOnlyList<ModeloFormulario> modelos,
        IReadOnlyList<Modalidade> modalidades)
    {
        List<FieldError> erros = [];
        FatoCandidato alterado = depois.Fatos.First(f => string.Equals(f.Codigo, codigo, StringComparison.Ordinal));

        foreach (FatoCandidato outro in depois.Fatos.Where(f => !string.Equals(f.Codigo, codigo, StringComparison.Ordinal)))
        {
            if (outro.FatoDeMembroAgregado == codigo)
            {
                FatoCandidato membroAntes = antes.Fatos.First(f => string.Equals(f.Codigo, codigo, StringComparison.Ordinal));
                string[] novos = Novas(
                    outro.MotivosPelosQuaisDeixaDeAgregar(alterado, depois),
                    outro.MotivosPelosQuaisDeixaDeAgregar(membroAntes, antes));
                Recusar(erros, "agregados", outro.Codigo, FatoCandidatoErrorCodes.AlteracaoQuebraAgregadoDoFato,
                    $"O agregado '{outro.Codigo}' deixaria de valer", novos);
            }
            else if (outro.RegrasPadrao.Any(r => r.FatosCitados.Contains(codigo, StringComparer.Ordinal)))
            {
                string[] novas = Novas(
                    ValidadorRegrasPadrao.Validar(outro, outro.RegrasPadrao, depois).Errors,
                    ValidadorRegrasPadrao.Validar(outro, outro.RegrasPadrao, antes).Errors);
                Recusar(erros, "regrasPadrao", outro.Codigo, FatoCandidatoErrorCodes.AlteracaoQuebraRegraPadraoDeOutroFato,
                    $"As regras padrão de '{outro.Codigo}' deixariam de valer", novas);
            }
        }

        foreach (ModeloFormulario modelo in modelos.Where(m => ConferenciaDoModelo.Vinculos(m.Conteudo).Fatos.Contains(codigo)))
        {
            string[] novas = Novas(ErrosDoModelo(modelo, depois), ErrosDoModelo(modelo, antes));
            Recusar(erros, "modelos", modelo.Codigo, FatoCandidatoErrorCodes.AlteracaoQuebraModeloDeFormulario,
                $"O modelo de formulário '{modelo.Codigo}' deixaria de valer", novas);
        }

        foreach (Modalidade modalidade in modalidades.Where(m => m.RegraDeAplicabilidade.FatosCitados.Contains(codigo, StringComparer.Ordinal)))
        {
            string[] novas = Novas(
                ConferenciaDaRegraDeAplicabilidade.Conferir(modalidade.RegraDeAplicabilidade, modalidade.RegraDeAplicabilidade, depois),
                ConferenciaDaRegraDeAplicabilidade.Conferir(modalidade.RegraDeAplicabilidade, modalidade.RegraDeAplicabilidade, antes));
            Recusar(erros, "modalidades", modalidade.Codigo.Valor, FatoCandidatoErrorCodes.AlteracaoQuebraRegraDeAplicabilidade,
                $"A regra de aplicabilidade da modalidade '{modalidade.Codigo.Valor}' deixaria de valer", novas);
        }

        return erros;
    }

    /// <summary>O conteúdo gravado do modelo contra o catálogo, com o que ele já vincula tolerado.</summary>
    private static List<FieldError> ErrosDoModelo(ModeloFormulario modelo, CatalogoDeFatos fatos)
    {
        CatalogoDoModelo catalogo = CatalogoDoModelo.De([.. fatos.Fatos]);
        ConteudoLido lido = ConteudoLido.Ler(modelo.ToView().Conteudo, catalogo.Formatos);
        return [.. lido.Erros, .. ConferenciaDoModelo.Conferir(lido, catalogo, new Dictionary<Guid, Contracts.VersaoTermoConsentimentoView>(), ConferenciaDoModelo.VinculosGravados(modelo.Conteudo))];
    }

    private static string[] Novas(IEnumerable<FieldError> depois, IEnumerable<FieldError> antes) =>
        [.. DiferencaDeRecusas.Novas(depois, antes).Select(static e => e.Error.Message).Distinct(StringComparer.Ordinal)];

    private static void Recusar(List<FieldError> erros, string citacao, string citante, string codigoErro, string inicio, string[] motivos)
    {
        if (motivos.Length > 0)
        {
            erros.Add(new($"citacoes.{citacao}.{citante}", new DomainError(codigoErro, $"{inicio}: {string.Join(" ", motivos)}")));
        }
    }
}
