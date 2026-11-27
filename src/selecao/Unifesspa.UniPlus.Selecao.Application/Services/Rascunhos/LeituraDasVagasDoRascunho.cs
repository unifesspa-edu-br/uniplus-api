namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê, uma vez por exportação, os cadastros vivos que as distribuições de vagas do rascunho citam: as ofertas de curso, as
/// modalidades, as referências demográficas e as regras, cada um pelos códigos citados. Sem distribuição, nenhum cadastro é consultado.
/// </summary>
public sealed class LeituraDasVagasDoRascunho(
    LeituraDosCadastrosDasVagasCitados vagas,
    LeituraDosCadastrosDoCatalogoCitados catalogo,
    TimeProvider relogio)
{
    private static readonly IdentificacaoNoDocumento SemIdentificacao = new(null, null, null, null, null, null, null);

    internal async Task<VagasDoRascunhoComCadastrosVivos> LerAsync(JsonObject documento, CancellationToken cancellationToken)
    {
        DeclaracoesDoDocumento declaracoes = new(SemIdentificacao, [])
        {
            Vagas = new(null, VagasDoDocumentoLidas.De(documento).Distribuicoes),
        };
        CodigosCitadosPorCadastro codigos = CodigosCitadosNoDocumento.Em(declaracoes);
        CadastrosCitadosNoDocumento cadastros = new(relogio.GetUtcNow())
        {
            VagasEExigencias = await vagas.LerAsync(codigos, cancellationToken).ConfigureAwait(false),
            Regras = await catalogo.LerRegrasAsync(codigos.Regras, cancellationToken).ConfigureAwait(false),
        };

        return new(declaracoes, cadastros);
    }
}
