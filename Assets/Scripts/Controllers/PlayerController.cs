using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class PlayerController : BaseController
{
    bool _stopAttack = true;   // 공격 가능 여부 
    bool _stopSkill = true;    // 스킬 가능 여부
    bool _isDiveRoll = false;    // 구르기 여부

    // LayerMask 변수
    int _mask = (1 << (int)Define.Layer.Ground) | (1 << (int)Define.Layer.Monster) | (1 << (int)Define.Layer.Npc);

    [SerializeField]
    private GameObject skillEffect1;

    [SerializeField]
    private GameObject skillEffect2;

    public override void Init()
    {
        WorldObjectType = Define.WorldObject.Player;
        
        anim = GetComponent<Animator>();

        Managers.Input.KeyAction -= OnKeyEvent;
        Managers.Input.KeyAction += OnKeyEvent;
        Managers.Input.MouseAction -= OnMouseEvent;
        Managers.Input.MouseAction += OnMouseEvent;

        // if (gameObject.GetComponentInChildren<UI_HPBar>() == null)  // 자식 객체안에 존재하는 지
        //     Managers.UI.MakeWorldSpaceUI<UI_HPBar>(transform);  // 체력바 생성
    }

    protected override void UpdateIdle()
    {
        // TODO : 가만히 있을때의 모션 있으면 사용
        anim.SetBool("IsMoving", false);
    }

    // 이동 메소드
    protected override void UpdateMoving()
    {
        if (_lockTarget != null){
            float distance = (_lockTarget.transform.position - transform.position).magnitude;
            if (distance <= 1f){
                // TODO : npc 상호작용
                State = Define.State.Idle;
                return;
            }
        }

        // 타겟 대상을 클릭했을 때 콜라이더 위쪽을 클릭하게 된다면 그쪽을 바라보고 달리기 때문에 y값을 0으로 준다.
        _destPos.y = 0; 

        // 도착 위치 벡터에서 플레이어 위치 벡터를 뺀다.
        Vector3 dir = _destPos - transform.position;

        // Vector3.magnitude = 벡터값의 길이
        if (dir.magnitude < 0.1f)
            State = Define.State.Idle;
        else{
            // 건물을 클릭하여 이동하면 건물 앞에 멈추기 (1.0f 거리에서 멈추기)
            Debug.DrawRay(transform.position + (Vector3.up * 0.5f), dir.normalized, Color.red);
            if (Physics.Raycast(transform.position + (Vector3.up * 0.5f), dir, 1.0f, LayerMask.GetMask("Block"))){
                if (Input.GetMouseButton(0) == false)
                    State = Define.State.Idle;
                return;
            }

            float moveDist = Mathf.Clamp(Managers.Game.MoveSpeed * Time.deltaTime, 0, dir.magnitude);
            
            transform.position += dir.normalized * moveDist;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 20f * Time.deltaTime);
        }
    }

    float _attackCloseTime = 0;
    protected override void UpdateAttack()
    {
        _attackCloseTime += Time.deltaTime;

        if (anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack") == false &&
            _attackCloseTime > 0.94f)
        {
            _onAttack = false;
            _stopAttack = true;
            _attackCloseTime = 0;
            State = Define.State.Idle;

            Debug.Log("공격 중단");
            return;
        }

        Vector3 dir = _destPos - transform.position;
        Quaternion quat = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Lerp(transform.rotation, quat, 20 * Time.deltaTime);
    }

    protected override void UpdateSkill()
    {
        if ((anim.GetCurrentAnimatorStateInfo(0).IsTag("Skill") &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f) ||
            _isDiveRoll == true)
        {
            skillEffect1.SetActive(false);
            skillEffect2.SetActive(false);

            if (_destPos == transform.position)
                State = Define.State.Idle;
            else
                State = Define.State.Moving;
        }
    }

    bool _onAttack = false;
    int doubleClickCheck = 0;
    void OnAttack()
    {
        // 콤보 체크
        if (_onAttack && doubleClickCheck == 0 &&
            anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime <= 0.91f &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.75f)
        {
            _onAttack = false;
            doubleClickCheck++;
        }

        // 공격!
        if (!_onAttack)
        {
            anim.SetTrigger("OnAttack");    // 공격 애니메이션
            State = Define.State.Attack;

            _onAttack = true;
            Invoke("DelayClick", 0.2f);
        }
    }
    void DelayClick() { doubleClickCheck = 0; }

    // 애니메이션에서 이벤트 호출 (공격 모션 끝날 쯤 실행 됨.)
    void OnHitEvent()
    {
        // 상대방껄 꺼내와서 스탯을 처리해주는게 좋다!
        // TODO : 공격 범위 지정
        // if (_lockTarget != null){
        //     Stat targetStat = _lockTarget.GetComponent<Stat>();
        //     targetStat._onAttacked(_stat);
        // }
    }
    
    // 마우스 클릭 메소드
    void OnMouseEvent(Define.MouseEvent evt)
    {
        switch(State){
            case Define.State.Moving:
                GetMouseEvent(evt);
                break;
            case Define.State.Idle:
                GetMouseEvent(evt);
                break;
            case Define.State.Attack:
                GetMouseEvent(evt);
                break;
            case Define.State.Skill:
                {
                    // TODO : 스킬 진행 중일 때 마우스 입력한다면 스킬이 끝날때까지 마우스 입력 못함.
                    // TODO : 스페이스바로 구르기 가능 
                }
                break;
        }
    }

    void GetMouseEvent(Define.MouseEvent evt)
    {
        // 메인 카메라에서 마우스가 가르키는 위치의 ray를 저장
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool raycastHit = Physics.Raycast(ray, out hit, 150f, _mask);
        Debug.DrawRay(Camera.main.transform.position, ray.direction * 100.0f, Color.red, 1.0f);
        
        switch (evt)
        {
            // 마우스를 클릭했을 때 [ 클릭한 위치로 이동 ]
            case Define.MouseEvent.RightDown:
                {
                    if (_isDiveRoll == true)
                        return;

                    _destPos = hit.point;   // 해당 좌표 저장
                    if (raycastHit && _stopAttack)
                    {
                        anim.SetBool("IsMoving", true);
                        State = Define.State.Moving;

                        if (hit.collider.gameObject.layer == (int)Define.Layer.Npc)
                            _lockTarget = hit.collider.gameObject;
                        else
                            _lockTarget = null;
                    }
                }
                break;
            // 마우스를 클릭 중일 때
            case Define.MouseEvent.RightPress:
                {
                    if (_stopAttack)
                    {
                        if (_lockTarget != null)
                            _destPos = _lockTarget.transform.position;
                        else if (raycastHit)
                            _destPos = hit.point;
                    }
                }
                break;
            // 왼쪽 클릭 시 공격
            case Define.MouseEvent.LeftDown:
                {
                    // TOOD : 첫번째 공격
                    anim.SetBool("IsMoving", false);
                    _stopAttack = false;
                    _destPos = hit.point;
                    State = Define.State.Attack;
                    OnAttack();
                }
                break;
        }
    }

    void OnKeyEvent()
    {
        OnSkill();
        OnDiveRoll();
    }

    void OnDiveRoll()
    {
        if (_isDiveRoll == true &&
            anim.GetCurrentAnimatorStateInfo(0).IsName("DIVEROLL") &&
            anim.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.9f)
        {
            _isDiveRoll = false;
            Managers.Game.MoveSpeed = 5;
        }

        if (Input.GetKeyDown(KeyCode.Space) && _isDiveRoll == false)
        {
            _isDiveRoll = true;
            State = Define.State.Moving;
            anim.CrossFade("DIVEROLL", 0.1f, -1, 0);
            Managers.Game.MoveSpeed = 8;
            skillEffect1.SetActive(false);
            skillEffect2.SetActive(false);
        }
    }

    // 스킬 사용
    void OnSkill()
    {
        // TODO : 스킬 관리코드 구현 (확장성)
        if (State == Define.State.Skill || _isDiveRoll == true)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool raycastHit = Physics.Raycast(ray, out hit, 150f, _mask);
        _destPos = hit.point;

        _destPos.y = 0; 
        Vector3 dir = _destPos - transform.position;
        transform.rotation = Quaternion.LookRotation(dir);

        if (Input.GetKeyDown(KeyCode.Q))
        {
            State = Define.State.Skill;
            anim.CrossFade("COMBO_1", 0.1f, -1, 0);
            skillEffect1.SetActive(true);
        }
        else if (Input.GetKeyDown(KeyCode.W))
        {
            State = Define.State.Skill;
            anim.CrossFade("COMBO_2", 0.1f, -1, 0);
            skillEffect2.SetActive(true);
        }
    }
}
