namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O fim da janela: o prazo, a falta de pendência e o encerramento manual, e a reconciliação que os antecede.</summary>
public sealed partial class JanelaDeMigracao
{
    /// <summary>
    /// O que toda operação confere antes de qualquer outra coisa: o prazo vencido materializa o fim da janela, a
    /// concessão que saiu da lista é reconciliada e a janela sem pendência se encerra. A recusa devolvida vem depois de a
    /// janela ter mudado, e quem chama precisa gravá-la.
    /// </summary>
    public DomainError? ConferirEstado(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Agora >= Prazo)
        {
            Materializar(contexto, FormaDoEncerramento.Prazo);
            return MigracaoErros.PrazoEncerrado();
        }

        Reconciliar(contexto);

        return EncerrarSeConcluida(contexto) ? MigracaoErros.JanelaEncerrada() : null;
    }

    /// <summary>
    /// Materializa o fim da janela no corte, que é o menor entre o instante e o prazo: pendência no corte é quem não tinha
    /// revogação nem vencimento até ali e não tinha situação final. Ela é revogada pelo sistema, no corte, se ainda não
    /// foi; a linha cuja concessão saiu antes do corte fica fora da lista. Janela já encerrada não muda.
    /// </summary>
    public void Materializar(ContextoDaMigracao contexto, FormaDoEncerramento forma, EncerramentoManual? manual = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (Encerrada)
        {
            return;
        }

        DateTimeOffset corte = contexto.Agora < Prazo ? contexto.Agora : Prazo;
        new FimDaJanelaNoCorte(this, contexto, corte).Encerrar();
        Encerrar(forma, corte, manual);
    }

    /// <summary>Leva a fora da lista a linha ainda aberta cuja concessão foi revogada ou venceu.</summary>
    public void Reconciliar(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (MigracaoDaConcessao linha in _migracoes.Where(static m => !m.Situacao.EhFinal()).ToList())
        {
            Concessao? concessao = contexto.Concessao(linha.ConcessaoId);
            if (concessao is null || !concessao.EstaAtiva(contexto.Agora))
            {
                RegistrarDoSistema(concessao, linha.ConcessaoId, SituacaoDaMigracao.ForaDaLista, contexto, contexto.Agora);
            }
        }
    }

    /// <summary>Encerra como concluída a janela que não tem mais pendência; devolve se encerrou.</summary>
    public bool EncerrarSeConcluida(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (Encerrada || Pendencias(contexto.Concessoes, contexto.Agora) > 0)
        {
            return false;
        }

        Encerrar(FormaDoEncerramento.Concluida, contexto.Agora, null);

        return true;
    }

    /// <summary>
    /// A gestão de acessos encerra a versão anterior antes do prazo, com motivo e a confirmação de que quem não migrou
    /// perde o acesso. A condição de a aprovação da versão nova estar concluída vale sempre, porque a janela só abre nela.
    /// </summary>
    public Result EncerrarManualmente(EncerramentoManual encerramento, bool confirmouExclusao, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(encerramento);
        ArgumentNullException.ThrowIfNull(contexto);

        string? motivo = MotivosDaVersao.Normalizar(encerramento.Motivo);
        if (RecusaDoEncerramento(motivo, confirmouExclusao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Materializar(contexto, FormaDoEncerramento.Manual, encerramento with { Motivo = motivo });

        return Result.Success();
    }

    private static DomainError? RecusaDoEncerramento(string? motivo, bool confirmouExclusao)
    {
        if (motivo is null)
        {
            return MigracaoErros.MotivoInvalido(MotivoMaxLength);
        }

        return confirmouExclusao ? null : MigracaoErros.ExclusaoNaoConfirmada();
    }

    internal void RegistrarDoSistema(
        Concessao? concessao, Guid concessaoId, SituacaoDaMigracao situacao, ContextoDaMigracao contexto, DateTimeOffset instante)
    {
        ComposicaoDoLote composicao = concessao is null
            ? new ComposicaoDoLote([], [], false)
            : contexto.ComposicaoDe(concessao);
        ResultadoDaMigracao resultado = situacao == SituacaoDaMigracao.ForaDaLista ? ResultadoDaMigracao.ForaDaLista : ResultadoDaMigracao.NaoMigrada;

        Registrar(concessaoId, situacao, composicao.ExigeProtecaoDeDados, instante);
        Acrescentar(DecisaoDaMigracao.DoSistema(this, concessaoId, resultado, composicao, instante));
    }

    private void Encerrar(FormaDoEncerramento forma, DateTimeOffset instante, EncerramentoManual? manual)
    {
        EncerradaEm = instante;
        Forma = forma;
        MotivoDoEncerramento = manual?.Motivo;
        EncerradaPorEmissor = manual?.Autoria.Pessoa.Emissor;
        EncerradaPorSub = manual?.Autoria.Pessoa.Sub;
        EncerradaPorNome = manual?.Autoria.Pessoa.Nome;
    }
}
