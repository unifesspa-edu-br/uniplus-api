namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

internal sealed class MontagemDasDependencias
{
    private readonly Dictionary<(TipoDeParteDaConfiguracao, string), ParteDaConfiguracao> _partes = [];
    private readonly HashSet<string> _dadosExistentes = new(StringComparer.Ordinal);
    private readonly List<DependenciaEntrePartes> _dependencias = [];
    private readonly Dictionary<ParteDaConfiguracao, long> _posicoes = [];
    private KeyValuePair<string, IReadOnlyList<string>>[] _derivadosDoSistema = [];

    internal IReadOnlyCollection<ParteDaConfiguracao> Partes => _partes.Values;
    internal IReadOnlyList<DependenciaEntrePartes> Dependencias => _dependencias;
    internal IReadOnlyDictionary<ParteDaConfiguracao, long> PosicoesDeclaradas => _posicoes;

    internal static MontagemDasDependencias Montar(IReadOnlyCollection<FatoColetado> itens,
        IReadOnlyCollection<ConfiguracaoDerivacaoFato> regras, IReadOnlyCollection<DocumentoExigido> documentos,
        IReadOnlyCollection<FormularioProcesso> formularios, IReadOnlyCollection<TermoExigidoFormulario> termos,
        IReadOnlyCollection<GrupoColetado> grupos)
    {
        MontagemDasDependencias montagem = new();
        montagem.RegistrarItens(itens, grupos);
        montagem.RegistrarDadosCalculados(itens, regras, documentos, formularios, termos, grupos);
        montagem.LigarItens(itens, grupos);
        montagem.LigarDadosCalculados(regras);
        montagem.LigarExigencias(documentos);
        montagem.LigarSecoes(formularios, itens, grupos);
        montagem.LigarTermos(termos);
        return montagem;
    }

    internal IReadOnlyList<ParteDaConfiguracao>? PrimeiroCiclo()
    {
        Dictionary<string, ParteDaConfiguracao> porIdentidade = Partes.ToDictionary(static p => p.Identidade, StringComparer.Ordinal);
        ILookup<ParteDaConfiguracao, ParteDaConfiguracao> citacoes = _dependencias.ToLookup(static d => d.Destino, static d => d.Origem);
        Dictionary<string, IReadOnlyCollection<string>> dependencias = Partes.Order(ParteDaConfiguracao.PorTipoECodigo)
            .ToDictionary(static p => p.Identidade, p => (IReadOnlyCollection<string>)[.. citacoes[p]
                .Order(ParteDaConfiguracao.PorTipoECodigo).Select(static c => c.Identidade)], StringComparer.Ordinal);
        return GrafoDeFatos.DetectarCiclo(dependencias) is { } ciclo ? [.. ciclo.Select(id => porIdentidade[id])] : null;
    }

    private static IEnumerable<(FatoColetado Item, FinalidadeFormulario Finalidade, int Ordem)> ItensNaPosicaoDeColeta(
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        itens.Select(static i => (i, i.Finalidade!.Value, i.Ordem))
            .Concat(grupos.SelectMany(static g => g.Subitens.Select(i => (i, g.Finalidade!.Value, g.Ordem))));

    private void RegistrarItens(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        foreach ((FatoColetado item, FinalidadeFormulario finalidade, int ordem) in ItensNaPosicaoDeColeta(itens, grupos))
        {
            _dadosExistentes.Add(item.FatoCodigo);
            _posicoes[Parte(TipoDeParteDaConfiguracao.Campo, item.FatoCodigo)] = PosicaoNaColeta.DoItem(finalidade, ordem);
            _posicoes[Parte(TipoDeParteDaConfiguracao.Dado, item.FatoCodigo)] = PosicaoNaColeta.DoItem(finalidade, ordem);
        }
    }

    private void RegistrarDadosCalculados(IEnumerable<FatoColetado> itens, IEnumerable<ConfiguracaoDerivacaoFato> regras,
        IEnumerable<DocumentoExigido> documentos, IEnumerable<FormularioProcesso> formularios,
        IEnumerable<TermoExigidoFormulario> termos, IEnumerable<GrupoColetado> grupos)
    {
        foreach (ConfiguracaoDerivacaoFato regra in regras)
        {
            _dadosExistentes.Add(regra.CodigoFato);
        }

        HashSet<string> citados = new(DadosCitados(itens, regras, documentos, formularios, termos, grupos), StringComparer.Ordinal);
        _derivadosDoSistema = [.. DerivadosDoSistema.Dependencias
            .Where(d => citados.Contains(d.Key) && d.Value.All(_dadosExistentes.Contains)).OrderBy(static d => d.Key, StringComparer.Ordinal)];
        foreach ((string codigo, _) in _derivadosDoSistema)
        {
            _dadosExistentes.Add(codigo);
        }
    }

    private static IEnumerable<string> DadosCitados(IEnumerable<FatoColetado> itens, IEnumerable<ConfiguracaoDerivacaoFato> regras,
        IEnumerable<DocumentoExigido> documentos, IEnumerable<FormularioProcesso> formularios,
        IEnumerable<TermoExigidoFormulario> termos, IEnumerable<GrupoColetado> grupos) =>
        itens.SelectMany(static i => i.FatosCitados)
            .Concat(grupos.SelectMany(static g => g.FatosCitados.Concat(g.Subitens.SelectMany(static s => s.FatosCitados))))
            .Concat(regras.SelectMany(static r => r.FatosCitados)).Concat(documentos.SelectMany(DadosDasCondicoes))
            .Concat(formularios.SelectMany(static f => f.Etapas).SelectMany(static e => e.FatosCitados))
            .Concat(termos.SelectMany(static t => t.FatosCitados));

    private void LigarItens(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        Dictionary<string, IReadOnlyCollection<string>> regrasDosGrupos = grupos
            .SelectMany(static g => g.Subitens.Select(s => (s.FatoCodigo, g.FatosCitados)))
            .ToDictionary(static p => p.FatoCodigo, static p => p.FatosCitados, StringComparer.Ordinal);
        foreach ((FatoColetado item, _, _) in ItensNaPosicaoDeColeta(itens, grupos))
        {
            ParteDaConfiguracao campo = Parte(TipoDeParteDaConfiguracao.Campo, item.FatoCodigo);
            _dependencias.Add(new(MotivoDaDependencia.InformaDado, campo, Parte(TipoDeParteDaConfiguracao.Dado, item.FatoCodigo)));
            IEnumerable<string> doGrupo = regrasDosGrupos.GetValueOrDefault(item.FatoCodigo) ?? [];
            LigarCitacoes(MotivoDaDependencia.CondicionaExibicao, item.FatosCitados.Concat(doGrupo).Distinct(StringComparer.Ordinal), campo);
        }
    }

    private void LigarDadosCalculados(IEnumerable<ConfiguracaoDerivacaoFato> regras)
    {
        IEnumerable<(string Codigo, IEnumerable<string> Citados)> calculados = regras.Select(static r => (r.CodigoFato, (IEnumerable<string>)r.FatosCitados))
            .Concat(_derivadosDoSistema.Select(static d => (d.Key, (IEnumerable<string>)d.Value)));
        foreach ((string codigo, IEnumerable<string> citados) in calculados)
        {
            LigarCitacoes(MotivoDaDependencia.CalculaDado, citados, Parte(TipoDeParteDaConfiguracao.Dado, codigo));
        }
    }

    private void LigarExigencias(IEnumerable<DocumentoExigido> documentos)
    {
        foreach (DocumentoExigido documento in documentos)
        {
            LigarCitacoes(MotivoDaDependencia.CondicionaExigencia, DadosDasCondicoes(documento),
                Parte(TipoDeParteDaConfiguracao.Exigencia, documento.Id.ToString("N")));
        }
    }

    private void LigarSecoes(IEnumerable<FormularioProcesso> formularios, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        foreach (FormularioProcesso formulario in formularios)
        {
            foreach (EtapaFormulario secao in formulario.Etapas.Where(static e => e.Exibicao is not null))
            {
                LigarSecao(formulario, secao, itens, grupos);
            }
        }
    }

    private void LigarSecao(FormularioProcesso formulario, EtapaFormulario secao, IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        ParteDaConfiguracao parte = Parte(TipoDeParteDaConfiguracao.Secao, CodigoNoFormulario(formulario.Finalidade, secao.Codigo));
        _posicoes[parte] = PosicaoNaColeta.DaSecao(formulario, secao, itens, grupos);
        LigarCitacoes(MotivoDaDependencia.CondicionaExibicao, secao.FatosCitados, parte);
        foreach (FatoColetado campo in CamposDaSecao(formulario, secao, itens, grupos))
        {
            _dependencias.Add(new(MotivoDaDependencia.CondicionaExibicao, parte, Parte(TipoDeParteDaConfiguracao.Campo, campo.FatoCodigo)));
        }
    }

    private static IEnumerable<FatoColetado> CamposDaSecao(FormularioProcesso formulario, EtapaFormulario secao,
        IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos) =>
        itens.Where(i => i.Finalidade == formulario.Finalidade && string.Equals(i.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))
            .Concat(grupos.Where(g => g.Finalidade == formulario.Finalidade && string.Equals(g.EtapaCodigo, secao.Codigo, StringComparison.Ordinal))
                .SelectMany(static g => g.Subitens));

    private void LigarTermos(IEnumerable<TermoExigidoFormulario> termos)
    {
        foreach (TermoExigidoFormulario termo in termos.Where(static t => t.FatosCitados.Count > 0))
        {
            ParteDaConfiguracao parte = Parte(TipoDeParteDaConfiguracao.Termo, CodigoNoFormulario(termo.Finalidade!.Value, termo.Codigo));
            _posicoes[parte] = PosicaoNaColeta.DoTermo(termo.Finalidade!.Value, termo.Ordem);
            LigarCitacoes(MotivoDaDependencia.CondicionaExibicao, termo.FatosCitados, parte);
        }
    }

    private void LigarCitacoes(MotivoDaDependencia motivo, IEnumerable<string> citados, ParteDaConfiguracao dependente)
    {
        foreach (string citado in citados.Where(_dadosExistentes.Contains))
        {
            _dependencias.Add(new(motivo, Parte(TipoDeParteDaConfiguracao.Dado, citado), dependente));
        }
    }

    private ParteDaConfiguracao Parte(TipoDeParteDaConfiguracao tipo, string codigo)
    {
        if (!_partes.TryGetValue((tipo, codigo), out ParteDaConfiguracao? parte))
        {
            parte = new(tipo, codigo);
            _partes.Add((tipo, codigo), parte);
        }

        return parte;
    }

    private static string CodigoNoFormulario(FinalidadeFormulario finalidade, string codigo) => $"{finalidade.Codigo()}.{codigo}";
    private static IReadOnlyCollection<string> DadosDasCondicoes(DocumentoExigido documento) =>
        [.. documento.Condicoes.Select(static c => c.Fato).Distinct(StringComparer.Ordinal)];
}
