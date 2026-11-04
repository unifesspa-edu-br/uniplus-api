namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.ProcessosSeletivos.Edicao;
using Domain.ProcessosSeletivos.Referencias;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Calcula a cópia do modelo para o processo sem mudá-lo: as etapas, os itens e os termos passam pelos
/// resolvedores da escrita do formulário (ADR-0125), e o estado final é conferido contra o catálogo antes
/// de o agregado receber a cópia. Antes, o modelo precisa estar ativo, servir ao tipo do processo e ter os
/// pressupostos coletados pela inscrição.
/// </summary>
internal sealed class PlanejamentoDaCopiaDoModelo(
    ProcessoSeletivo processo,
    ModeloFormularioView modelo,
    CadastrosDaAplicacaoDoModelo cadastros)
{
    private readonly RelatorioDaAplicacaoDoModelo _relatorio = new();
    private readonly FinalidadeFormulario _finalidade = EstruturaFormulario.FinalidadeDoToken(modelo.Finalidade);

    public async Task<Result<PlanoDaCopiaDoModelo>> PlanejarAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> catalogo = await cadastros.Fatos.ListarAsync(cancellationToken).ConfigureAwait(false);
        ContextoDoCatalogo contexto = ContextoDoCatalogo.De(processo, catalogo);
        ConteudoDoModeloInput conteudo = modelo.Conteudo;
        FormularioCopiado formulario = new CopiaDoFormularioDoModelo(processo, _finalidade, contexto, _relatorio).Copiar(conteudo);
        TermosSelecionados termos = await new CopiaDosTermosDoModelo(cadastros.Termos, _relatorio)
            .SelecionarAsync(conteudo.Termos ?? [], cancellationToken).ConfigureAwait(false);

        CopiaResolvida copia = await ResolverAsync(contexto, conteudo, formulario, termos, cancellationToken).ConfigureAwait(false);
        List<FieldError> recusas = [.. formulario.Recusas, .. copia.Recusas];
        if (recusas.Count > 0)
        {
            return Result<PlanoDaCopiaDoModelo>.ValidationFailure(recusas);
        }

        Result vinculoNovo = ConferirVinculoNovo(contexto, formulario, copia);
        return vinculoNovo.IsFailure
            ? Result<PlanoDaCopiaDoModelo>.ValidationFailure([.. vinculoNovo.Errors.Select(static e => PosicaoNoModelo.NoModelo(e))])
            : Result<PlanoDaCopiaDoModelo>.Success(NovoPlano(conteudo, formulario, termos, copia, catalogo));
    }

    // Os derivados citados vêm das partes que entram na cópia e das regras padrão do catálogo, e o universo
    // da configuração final é o que o processo coleta e deriva depois dela. O termo cita também o derivado do
    // sistema cujas dependências o processo coleta depois da cópia; a regra de derivação, não.
    private async Task<CopiaResolvida> ResolverAsync(
        ContextoDoCatalogo contexto,
        ConteudoDoModeloInput conteudo,
        FormularioCopiado formulario,
        TermosSelecionados termos,
        CancellationToken cancellationToken)
    {
        CopiaDasDerivacoesDoModelo derivacoes = new(processo, contexto, cadastros.Fatos, _relatorio);
        string[] citados = FatosCitadosPeloModelo.Todos(conteudo, formulario, termos);
        DerivacoesEscolhidas escolhidas = await derivacoes.EscolherAsync(citados, cancellationToken).ConfigureAwait(false);
        FieldError? semDistribuicaoDeVagas = derivacoes.ConferirModalidade(citados, escolhidas.ACopiar);

        HashSet<string> coletados = new(
            processo.FatosColetados.Select(static f => f.FatoCodigo).Concat(formulario.Itens.Itens.Select(static i => i.FatoCodigo)),
            StringComparer.Ordinal);
        HashSet<string> universo = new(
            coletados
                .Concat(processo.DerivacoesEfetivas().Select(static r => r.CodigoFato))
                .Concat(escolhidas.ACopiar.Select(static d => d.CodigoFato)),
            StringComparer.Ordinal);
        DerivacoesResolvidas derivacoesResolvidas = derivacoes.Resolver(escolhidas.ACopiar, universo);
        TermosResolvidos termosResolvidos = CopiaDosTermosDoModelo.Resolver(
            termos, contexto, new HashSet<string>(universo.Concat(VocabularioDeFatos.DerivadosDoSistemaResolvidos(coletados)), StringComparer.Ordinal));
        List<FieldError> recusas = [.. escolhidas.Recusas];
        if (semDistribuicaoDeVagas is not null)
        {
            recusas.Add(semDistribuicaoDeVagas);
        }

        recusas.AddRange(derivacoesResolvidas.Recusas);
        recusas.AddRange(termosResolvidos.Recusas);
        return new CopiaResolvida(derivacoesResolvidas.Derivacoes, termosResolvidos.Termos, recusas);
    }

    // O valor desativado no catálogo é recusado como vínculo novo; o que o processo já citava continua.
    private Result ConferirVinculoNovo(ContextoDoCatalogo contexto, FormularioCopiado formulario, CopiaResolvida copia) =>
        ConferenciaDeVinculoNovo.Conferir(
            contexto.Fatos,
            ReferenciasVivas.Vinculos(EstadoDaConfiguracao.De(processo)),
            VinculosDeFatos.De(
                formulario.Itens.Itens.Concat(formulario.Grupos.Grupos.SelectMany(static g => g.Subitens)).Select(static i => i.FatoCodigo)
                    .Concat(copia.Derivacoes.Select(static d => d.CodigoFato)),
                CondicoesDaCopia(formulario, copia),
                copia.Derivacoes.SelectMany(static d => d.CodigosContribuidos)));

    private static IEnumerable<(string Fato, JsonElement Valor)> CondicoesDaCopia(FormularioCopiado formulario, CopiaResolvida copia) =>
        formulario.Itens.Itens.SelectMany(static i => i.Condicoes)
            .Concat(formulario.Grupos.Grupos.SelectMany(static g => g.Condicoes))
            .Concat(formulario.Etapas.Etapas.SelectMany(static e => e.Condicoes))
            .Concat(copia.Termos.SelectMany(static t => t.Condicoes))
            .Select(static c => (c.Fato, c.Valor))
            .Concat(copia.Derivacoes.SelectMany(static d => d.Regras).SelectMany(static r => r.Condicoes).Select(static c => (c.Fato, c.Valor)));

    private PlanoDaCopiaDoModelo NovoPlano(
        ConteudoDoModeloInput conteudo,
        FormularioCopiado formulario,
        TermosSelecionados termos,
        CopiaResolvida copia,
        IReadOnlyList<FatoCandidatoView> catalogo) =>
        new(
            new CopiaDeModeloDeFormulario(
                _finalidade, conteudo.Titulo, formulario.Etapas.Etapas, formulario.Itens.Itens, formulario.Grupos.Grupos,
                copia.Termos, copia.Derivacoes, modelo.Id, modelo.Codigo),
            new IndicesNoModelo(formulario.Itens.IndicesNoModelo, formulario.Grupos.IndicesNoModelo, termos.IndicesNoModelo),
            _relatorio,
            catalogo);

    private sealed record CopiaResolvida(
        List<ConfiguracaoDerivacaoFato> Derivacoes, List<TermoExigidoFormulario> Termos, List<FieldError> Recusas);
}
