namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As fases e as etapas que admitem recurso, com os procedimentos e as bancas que os testes de quem julga o recurso declaram, e as
/// cópias de tipo de banca que avaliam o que o teste escolher.
/// </summary>
internal static class RecursosParaQuemJulga
{
    internal static ProcedimentoDeRecursoDeclarado Procedimento(TipoMotivoDeDecisao tipo, AlvoDoProcedimentoDeRecurso? alvo = null) =>
        ProcedimentoDeRecursoDeclarado.De(tipo, alvo, ReferenciaRegra.De(RegraProcedimentoRecursoCodigo.Recalculo, "v1", new string('7', 64)));

    internal static ModeloDeFase FaseComRecurso(
        string codigo, ProcedimentoDeRecursoDeclarado[] procedimentos, bool instanciaSuperior = false, params BancaRequerida[] bancas)
    {
        ProdutoDaFase preliminar = ModeloDeFase.Preliminar("RESULTADO");
        RegraRecursoFase regra = RegraRecursoFase.Criar(
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
            Parametros(instanciaSuperior),
            preliminar.Id,
            ProcedimentosDeRecurso.De(procedimentos)).Value!;

        return new ModeloDeFase(codigo, 1) { Produtos = [preliminar], RegraRecurso = regra, Bancas = bancas };
    }

    internal static ModeloDeEtapa EtapaComRecurso(
        string nome, string codigoDaFase, ProcedimentoDeRecursoDeclarado[] procedimentos, bool instanciaSuperior = false, params BancaDaEtapa[] bancas) =>
        new(nome)
        {
            FaseCodigo = codigoDaFase,
            EmiteParecerIndividual = true,
            Recursos = [RecursoDaEtapa.Criar(
                AncoraDoRecurso.CienciaIndividual,
                ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('a', 64)),
                Parametros(instanciaSuperior),
                Guid.Empty,
                ProcedimentosDeRecurso.De(procedimentos)).Value!],
            Bancas = bancas,
        };

    /// <summary>A cópia do tipo que avalia o mérito e julga os tipos de decisão dados.</summary>
    internal static TipoDeBancaCopiado Avaliadora(
        TipoDeBanca codigo, bool encerraRecursoNaPrimeiraInstancia = false, params TipoMotivoDeDecisao[] julga) =>
        AvaliadoraComMarcas(codigo, false, encerraRecursoNaPrimeiraInstancia, julga);

    /// <summary>A cópia do tipo que avalia o mérito, com as duas marcas do tipo à escolha do teste.</summary>
    internal static TipoDeBancaCopiado AvaliadoraComMarcas(
        TipoDeBanca codigo, bool exigeDuasOuMaisBancas, bool encerraRecursoNaPrimeiraInstancia, params TipoMotivoDeDecisao[] julga) =>
        new(Guid.CreateVersion7(), codigo, codigo.Rotulo(), PapelDaBanca.AvaliaOMerito, exigeDuasOuMaisBancas, encerraRecursoNaPrimeiraInstancia, julga);

    internal static BancaRequerida NaFase(TipoDeBancaCopiado tipo) => BancaRequerida.Criar(tipo, [], BancasDeTeste.Regra());

    internal static BancaDaEtapa NaEtapa(TipoDeBancaCopiado tipo) => BancaDaEtapa.Criar(tipo, BancasDeTeste.Regra());

    private static ParametrosDeclaradosDoRecurso Parametros(bool instanciaSuperior) => instanciaSuperior
        ? ParametrosDoRecursoDeTeste.ComInstanciaSuperior()
        : ParametrosDoRecursoDeTeste.Declarados(48m, UnidadePrazo.Horas);
}
