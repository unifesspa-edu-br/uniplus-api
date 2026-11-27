namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ModelosFormulario;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Leituras;

/// <summary>
/// O modelo de formulário no formato que o certame divulgado e o rascunho do processo servem
/// (ADR-0139), para a simulação do administrador interpretar o modelo antes de aplicá-lo; recusa com o erro
/// de modelo não encontrado quando o modelo não existe.
/// </summary>
public sealed record ObterFormularioApresentadoDoModeloQuery(Guid Id) : IQuery<Result<FormularioApresentado>>;

/// <summary>
/// Projeta a simulação do modelo — o conteúdo como aplicado, com o catálogo vivo — no formato do
/// apresentado: as regras pela mesma definição da pré-visualização do modelo, recortadas como um
/// formulário só; as opções que o catálogo oferece a cada campo; e o conteúdo da versão escolhida de cada
/// termo. As opções que só existem no processo — as que ele declara, as modalidades, os municípios do
/// FADA — saem nulas: no modelo, ainda não há processo que as oferte. A condição de atendimento, o
/// recurso de acessibilidade, o tipo de deficiência e os municípios do FADA são a exceção: oferecem o
/// cadastro institucional, que o processo recorta.
/// </summary>
public static class ObterFormularioApresentadoDoModeloQueryHandler
{
    public static async Task<Result<FormularioApresentado>> Handle(
        ObterFormularioApresentadoDoModeloQuery query,
        IModeloFormularioRepository repository,
        IFatoCandidatoRepository fatoRepository,
        OpcoesDoCadastroInstitucional cadastro,
        ITermoConsentimentoRepository termoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(fatoRepository);
        ArgumentNullException.ThrowIfNull(cadastro);
        ArgumentNullException.ThrowIfNull(termoRepository);

        ModeloFormulario? modelo = await repository.ObterPorIdParaLeituraAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return modelo is null
            ? Result<FormularioApresentado>.Failure(new DomainError(ModeloFormularioErrorCodes.NaoEncontrado, "Modelo de formulário não encontrado."))
            : Result<FormularioApresentado>.Success(
                await ApresentarAsync(modelo, fatoRepository, cadastro, termoRepository, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<FormularioApresentado> ApresentarAsync(
        ModeloFormulario modelo,
        IFatoCandidatoRepository fatoRepository,
        OpcoesDoCadastroInstitucional cadastro,
        ITermoConsentimentoRepository termoRepository,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidato> fatos = await fatoRepository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        SimulacaoDoModelo simulacao = await SimulacaoDoModelo.MontarAsync(modelo, fatos, cadastro, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TermoApresentado> termos = await TermosAsync(modelo.Id, simulacao.Conteudo, termoRepository, cancellationToken).ConfigureAwait(false);
        return Montar(modelo.Declaracao, simulacao, termos);
    }

    private static FormularioApresentado Montar(DeclaracaoDoFormulario declaracao, SimulacaoDoModelo simulacao, IReadOnlyList<TermoApresentado> termos)
    {
        ConteudoDoModelo conteudo = simulacao.Conteudo;
        RecorteDoFormulario recorte = RecorteDoFormulario.DaDefinicaoInteira(simulacao.Definicao(), simulacao.Ofertas);
        DescricaoDosPressupostos descricao = new([], VocabularioDoCatalogo.ApresentacaoDos(simulacao.Fatos, recorte.Pressupostos), [.. simulacao.Agregados]);

        return new FormularioApresentado(
            conteudo.Titulo,
            [.. conteudo.Paginas.OrderBy(static e => e.Ordem).Select(Secao)],
            termos,
            new ColetaApresentada(
                [.. conteudo.Itens.OrderBy(static i => i.Ordem).Select(i => Campo(declaracao, i, simulacao.Opcoes))],
                [.. conteudo.Grupos.OrderBy(static g => g.Ordem).Select(g => Grupo(declaracao, g, simulacao.Opcoes))]),
            recorte.Regras,
            new PressupostosDoFormulario(descricao.Descrever(recorte.Pressupostos), null, null));
    }

    private static async Task<IReadOnlyList<TermoApresentado>> TermosAsync(
        Guid modeloId, ConteudoDoModelo conteudo, ITermoConsentimentoRepository termoRepository, CancellationToken cancellationToken)
    {
        List<TermoApresentado> termos = [];
        foreach (TermoDoModelo termo in conteudo.Termos.OrderBy(static t => t.Ordem))
        {
            termos.Add(await TermoAsync(modeloId, termo, termoRepository, cancellationToken).ConfigureAwait(false));
        }

        return termos;
    }

    private static async Task<TermoApresentado> TermoAsync(
        Guid modeloId, TermoDoModelo termo, ITermoConsentimentoRepository termoRepository, CancellationToken cancellationToken)
    {
        TermoConsentimento? doCatalogo = await termoRepository.ObterPorIdParaLeituraAsync(termo.TermoId, cancellationToken).ConfigureAwait(false);
        TermoConsentimentoVersao? versao = doCatalogo?.Versoes.FirstOrDefault(v => v.Id == termo.VersaoId);
        if (doCatalogo is null || versao is null)
        {
            // O modelo confere a versão contra o catálogo ao gravar; uma versão que sumiu depois é
            // defeito do cadastro, não estado que a simulação deva esconder.
            throw new InvalidOperationException($"A versão {termo.VersaoId} do termo '{termo.Codigo}' do modelo {modeloId} não está no catálogo.");
        }

        ConteudoDoTermoDto conteudo = new(doCatalogo.Nome, versao.Texto, versao.BaseLegal, CodigoComRotulo<FormaAceite>.De(versao.FormaAceite), versao.Hash);
        return new TermoApresentado(termo.Codigo, termo.Codigo, termo.Ordem, termo.TermoId, termo.VersaoId, conteudo);
    }

    private static PaginaApresentada Secao(PaginaDoModelo pagina) => new(
        pagina.Codigo,
        pagina.Tipo == TipoPaginaFormulario.Secao ? pagina.Codigo : null,
        pagina.Ordem,
        pagina.Tipo.Codigo(),
        pagina.Bloco?.Codigo(),
        new ApresentacaoDaPaginaDto(pagina.Titulo, pagina.Descricao, pagina.Aviso));

    private static GrupoApresentado Grupo(
        DeclaracaoDoFormulario declaracao, GrupoDoModelo grupo, IReadOnlyDictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes) => new(
        grupo.Codigo,
        grupo.Ordem,
        grupo.PaginaCodigo,
        grupo.Rotulo,
        new OcorrenciasDoGrupoDto(grupo.Ocorrencias.Minimo, grupo.Ocorrencias.Maximo, grupo.Ocorrencias.IncluiCandidato),
        [.. grupo.Subitens.OrderBy(static s => s.Ordem).Select(s => Campo(declaracao, s, opcoes))]);

    private static CampoApresentado Campo(
        DeclaracaoDoFormulario declaracao, ItemDoModelo item, IReadOnlyDictionary<string, IReadOnlyList<ValorSelecionavel>?> opcoes) => new(
        item.FatoCodigo,
        item.Ordem,
        item.PaginaCodigo,
        new ApresentacaoDoCampoDto(
            item.Apresentacao.Rotulo,
            item.Apresentacao.TipoDeCampo.Codigo(),
            ConjuntoBasicoDaInscricao.AjudaDoCampo(declaracao, item.FatoCodigo, item.Apresentacao.Ajuda),
            item.Apresentacao.PedirConfirmacao),
        item.Formato,
        opcoes.GetValueOrDefault(item.FatoCodigo));
}
