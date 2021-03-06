﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{

    #region variables
    public string type;
    [Range(0, 100)]
    public float speed;
    [Range(0, 100)]
    public float rotationSpeed;
    [Range(0, 5)]
    public float pathfollowingPrecision;
    [Range(0, 10)]
    public int chasingRange;
    [Range(0.1f, 10f)]
    public float pathfindingRandomMaxWaitTime;
    [Range(0, 5)]
    public float attackRange;
    [Range(0, 100)]
    public int damage;
    [Range(1, 100)]
    public int health;
    public bool drawPath;

    Transform target;
    Rigidbody myRigidbody;
    Vector3 velocity;
    Pathfinding pathfinding;
    LinkedList<Vector3> path;
    Vector3 lastPathfindedPosition;
    Vector3 nextStep;
    Animator animator;
    EnemySpawner spawner;
    int currentHealth;
    bool pathfindingScheduled;
    float pathfindingPrecision;

    enum State
    {
        Moving,
        Attacking,
        Chasing,
        Idle
    }
    State currentState;
    State CurrentState
    {
        get { return currentState; }
        set
        {
            if (value != currentState)
            {
                switch (value)
                {
                    case State.Idle:
                        animator.SetInteger("State", 0);
                        velocity = Vector3.zero;
                        path = null;
                        break;
                    case State.Moving:
                        animator.SetInteger("State", 1);
                        break;
                    case State.Attacking:
                        animator.SetInteger("State", 2);
                        velocity = Vector3.zero;
                        path = null;
                        break;
                    case State.Chasing:
                        animator.SetInteger("State", 1);
                        path = null;
                        break;
                }
                currentState = value;
            }
        }
    }
    List<EnemyController> enemiesNearby;
    #endregion

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        target = GameObject.FindGameObjectWithTag("Player").transform;
        myRigidbody = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        CurrentState = State.Idle;
        lastPathfindedPosition = target.transform.position;
        nextStep = transform.position;

        enemiesNearby = new List<EnemyController>();
        pathfindingScheduled = false;

        currentHealth = health;

        pathfinding = GameObject.FindGameObjectWithTag("Map").GetComponent<MapGenerator>().pathfinding;
        StartCoroutine(Pathfind());
    }

    void Update()
    {
        if(ApproximatedDistance(transform.position, lastPathfindedPosition) > 25)
            pathfindingPrecision = Mathf.Max(chasingRange,
                (Mathf.Abs(target.position.x - transform.position.x) + Mathf.Abs(target.position.z - transform.position.z)) / 10);

        switch (CurrentState)
        {
            case State.Idle:
                if (ApproximatedDistance(transform.position, target.position) < attackRange * attackRange)
                    CurrentState = State.Attacking;
                else if (ApproximatedDistance(transform.position, target.position) < chasingRange * chasingRange)
                {
                    CurrentState = State.Chasing;
                }
                else if (ApproximatedDistance(lastPathfindedPosition, target.position) > pathfindingPrecision * pathfindingPrecision)
                {
                    StartCoroutine(Pathfind());
                }
                break;

            case State.Moving:
                if (ApproximatedDistance(transform.position, target.position) < attackRange * attackRange)
                {
                    CurrentState = State.Attacking;
                }
                else if (ApproximatedDistance(transform.position, target.position) < chasingRange * chasingRange)
                {
                    CurrentState = State.Chasing;
                }
                else
                {
                    if (ApproximatedDistance(lastPathfindedPosition, target.position) > pathfindingPrecision * pathfindingPrecision)
                    {
                        StartCoroutine(Pathfind());
                    }

                    if (ApproximatedDistance(nextStep, transform.position) < pathfollowingPrecision * pathfollowingPrecision)
                    {
                        if (path != null && path.Count > 0)
                        {
                            nextStep = path.First.Value;
                            path.RemoveFirst();
                        }
                        else
                        {
                            CurrentState = State.Idle;
                            StartCoroutine(Pathfind());
                        }                            
                    }

                    velocity = nextStep - transform.position;
                    velocity.y = 0;
                    velocity = velocity.normalized * speed;
                    if (velocity.magnitude != 0)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), rotationSpeed * Time.deltaTime);
                }
                
                break;

            case State.Attacking:
                float approxDistance = ApproximatedDistance(transform.position, target.position);

                if ( approxDistance > attackRange * attackRange)
                {
                    if (approxDistance > chasingRange * chasingRange)
                    {
                        StartCoroutine(Pathfind());
                    }
                    else
                    {
                        CurrentState = State.Chasing;
                    }
                }

                Vector3 lookAt = target.position - transform.position;
                lookAt.y = 0;
                if(lookAt.magnitude != 0)
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(lookAt), rotationSpeed * Time.deltaTime);
                break;

            case State.Chasing:
                if (ApproximatedDistance(target.position, transform.position) < attackRange * attackRange)
                {
                    CurrentState = State.Attacking;
                }
                else if (ApproximatedDistance(target.position, transform.position) > chasingRange * chasingRange)
                {
                    StartCoroutine(Pathfind());
                }
                else
                {
                    velocity = target.position - transform.position;
                    velocity.y = 0;
                    velocity = velocity.normalized * speed;
                    if (velocity.magnitude != 0)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), rotationSpeed * Time.deltaTime);
                }

                break;
        }

        if (drawPath && path != null && path.Count > 0)
        {
            Vector3[] p = new Vector3[path.Count];
            path.CopyTo(p, 0);
            for (int i = 0; i < p.Length - 1; i++)
            {
                Debug.DrawLine(p[i], p[i + 1]);
            }
            Debug.DrawLine(nextStep + new Vector3(2.5f, 0, 2.5f), nextStep + new Vector3(-2.5f, 0, -2.5f), Color.red);
            Debug.DrawLine(nextStep + new Vector3(2.5f, 0, -2.5f), nextStep + new Vector3(-2.5f, 0, 2.5f), Color.red);
        }
    }

    void FixedUpdate()
    {
        if (CurrentState == State.Moving || CurrentState == State.Chasing)
        {
            myRigidbody.MovePosition(myRigidbody.position + velocity * Time.fixedDeltaTime);
        }
    }

    IEnumerator Pathfind()
    {
        if(pathfindingScheduled)
            yield break;

        pathfindingScheduled = true;
        lastPathfindedPosition = target.position;
        float waitSeconds = pathfindingRandomMaxWaitTime;
        bool pathFound = false;

        if (pathfinding.AreNeighbours(lastPathfindedPosition, target.position) && path != null)
        {
            path.AddLast(target.position);
            pathFound = true;
            waitSeconds = 0;
        }

        yield return new WaitForSeconds(Random.Range(0, waitSeconds));
        
        lastPathfindedPosition = target.position;

        if (pathfinding == null)
        {
            pathfinding = GameObject.FindGameObjectWithTag("Map").GetComponent<MapGenerator>().pathfinding;
        }        

        if(!pathFound)
            foreach (var enemy in enemiesNearby)
            {
                if (enemy.lastPathfindedPosition == lastPathfindedPosition && enemy.path != null)
                {
                    path = new LinkedList<Vector3>();
                    foreach (var pos in enemy.path)
                    {
                        path.AddLast(pos);
                    }
                    pathFound = true;
                    break;
                }
            }

        if (!pathFound)
        {
            path = pathfinding.GetPath(transform.position, lastPathfindedPosition);
        }

        if (path != null && path.Count > 1)
        {
            CurrentState = State.Moving;

            path.RemoveLast();
            path.AddLast(lastPathfindedPosition);

            nextStep = path.First.Value;
            path.RemoveFirst();            
        }
        else
        {
            CurrentState = State.Idle;
        }

        pathfindingScheduled = false;
    }

    float ApproximatedDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Pow(a.x - b.x, 2) + Mathf.Pow(a.z - b.z, 2);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
            enemiesNearby.Add(other.GetComponent<EnemyController>());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag == "Enemy")
            enemiesNearby.Remove(other.GetComponent<EnemyController>());
    }

    public bool IsAttacking()
    {
        return currentState == State.Attacking;
    }

    public void SetSpawner(EnemySpawner spawner)
    {
        this.spawner = spawner;
    }

    public void Hit(int damage)
    {
        currentHealth -= damage;

        if (currentHealth <= 0)
            spawner.DiedEnemy(this.gameObject);
    }
}