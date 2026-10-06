namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O modelo de formulário no formato que o certame divulgado e o rascunho do processo servem
/// (ADR-0139), para a simulação do administrador interpretar o modelo antes de aplicá-lo; nulo quando
/// o modelo não existe.
/// </summary>
public sealed record ObterFormularioRenderizavelDoModeloQuery(Guid Id) : IQuery<FormularioRenderizavel?>;

/// <summary>
/// Projeta o conteúdo do modelo como aplicado — o de inscrição, com o conjunto básico — com o catálogo
/// vivo: as regras pela mesma definição da pré-visualização do modelo, recortadas como um formulário só; as opções que o catálogo oferece a
/// cada campo; e o conteúdo da versão escolhida de cada termo. As opções que só existem no processo
/// — as que ele declara, as modalidades, os municípios do bônus — saem nulas: no modelo, ainda não há
/// processo que as oferte. A condição de atendimento e o tipo de deficiência são a exceção: oferecem o
/// cadastro institucional, que a oferta do processo recorta.
/// </summary>
public static class ObterFormularioRenderizavelDoModeloQueryHandler
{
    public static async Task<FormularioRenderizavel?> Handle(
        ObterFormularioRenderizavelDoModeloQuery query,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        ICondicaoAtendimentoRepository condicaoRepository,
        ITipoDeficienciaRepository tipoDeficienciaRepository,
        ITermoConsentimentoRepository termoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(condicaoRepository);
        ArgumentNullException.ThrowIfNull(tipoDeficienciaRepository);
        ArgumentNullException.ThrowIfNull(termoRepository);

        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        if (modelo is null)
        {
            return null;
        }

        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        // O formulário que o candidato verá é o do modelo aplicado: o de inscrição, com o conjunto básico.
        ConteudoDoModelo conteudo = EscritaDoModelo.ComoAplicado(modelo, fatos);
        IReadOnlyList<DefinicaoAgregado> agregados = VocabularioDoCatalogo.AgregadosDosGrupos(fatos, conteudo.Grupos);
        Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes = VocabularioDoCatalogo.OpcoesDoModelo(fatos, conteudo);
        await OpcoesDoCadastroInstitucional.CompletarAsync(opcoes, condicaoRepository, tipoDeficienciaRepository, cancellationToken).ConfigureAwait(false);

        List<TermoRenderizavel> termos = [];
        foreach (TermoDoModelo termo in conteudo.Termos.OrderBy(static t => t.Ordem))
        {
            TermoConsentimento? doCatalogo = await termoRepository.ObterPorIdParaLeituraAsync(termo.TermoId, cancellationToken).ConfigureAwait(false);
            TermoConsentimentoVersao? versao = doCatalogo?.Versoes.FirstOrDefault(v => v.Id == termo.VersaoId);
            if (doCatalogo is null || versao is null)
            {
                // O modelo confere a versão contra o catálogo ao gravar; uma versão que sumiu depois é
                // defeito do cadastro, não estado que a simulação deva esconder.
                throw new InvalidOperationException($"A versão {termo.VersaoId} do termo '{termo.Codigo}' do modelo {modelo.Id} não está no catálogo.");
            }

            termos.Add(new TermoRenderizavel(
                termo.Codigo, termo.Codigo, termo.Ordem, termo.TermoId, termo.VersaoId, doCatalogo.Nome, versao.Texto, versao.BaseLegal,
                FormasAceite.ParaTokenCanonico(versao.FormaAceite), versao.Hash));
        }

        return FormularioRenderizavel.Montar(
            EstruturaFormulario.ParaToken(modelo.Finalidade),
            conteudo.Titulo,
            [.. conteudo.Etapas.OrderBy(static e => e.Ordem).Select(static e => new SecaoRenderizavel(
                e.Codigo,
                e.Tipo == TipoEtapaFormulario.Secao ? e.Codigo : null,
                e.Ordem,
                EstruturaFormulario.ParaToken(e.Tipo),
                EstruturaFormulario.ParaToken(e.Bloco),
                e.Titulo,
                e.Descricao,
                e.Aviso))],
            termos,
            [.. conteudo.Itens.OrderBy(static i => i.Ordem).Select(i => Campo(i, opcoes))],
            [.. conteudo.Grupos.OrderBy(static g => g.Ordem).Select(g => new GrupoRenderizavel(
                g.Codigo, g.Ordem, g.EtapaCodigo, g.Rotulo, g.Minimo, g.Maximo, g.IncluiCandidato,
                [.. g.Subitens.OrderBy(static s => s.Ordem).Select(s => Campo(s, opcoes))]))],
            RecorteDaFinalidade.DoFormulario(conteudo.ParaAvaliacao(VocabularioDoCatalogo.RegrasDeDerivacao(fatos), agregados), VocabularioDoCatalogo.Ofertas(opcoes)),
            camposDosOutrosFormularios: [],
            agregados,
            dataReferenciaFatos: null);
    }

    private static CampoRenderizavel Campo(ItemDoModelo item, Dictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes) => new(
        item.FatoCodigo,
        item.Ordem,
        item.Rotulo,
        item.TipoRenderizacao.ToCodigo(),
        opcoes.GetValueOrDefault(item.FatoCodigo),
        item.EtapaCodigo,
        item.Formato,
        item.Ajuda,
        item.PedirConfirmacao);
}
